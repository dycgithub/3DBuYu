using System.Collections;
using System.Collections.Generic;
using Services;
using R3;
using UnityEngine;
using UnityEngine.Audio;

namespace GameSystem
{
    public partial class AudioManager : MonoBehaviour
    {
        [Header("音频混音器")]
        public AudioMixer mainMixer;
        public string masterVolumeParam = "MasterVolume";
        public string bgmVolumeParam = "BGMVolume";
        public string sfxVolumeParam = "SFXVolume";

        [Header("音效")]
        public int sfxPoolSize = 10;
        [Min(1)] public int maximumSfxSourceCount = 32;
        public List<NamedAudioClip> sfxClips = new List<NamedAudioClip>();

        [Header("设置")]
        [Range(0f, 1f)]
        public float defaultVolume = 0.8f;
        public bool lowerVolumeOnPause = true;
        [Range(0f, 1f)]
        public float pauseVolumeMultiplier = 0.3f;

        private IGamePauseService _pauseService;

        private readonly List<AudioSource> sfxSources = new();
        private readonly Queue<AudioSource> sfxPool = new();
        private readonly Dictionary<AudioSource, int> sfxLeaseIds = new();
        private readonly Dictionary<AudioSource, float> sfxLocalVolumes = new();
        private readonly Dictionary<string, AudioClip> sfxDictionary = new();
        private const double TurnSilenceSeconds = 2d;
        private readonly Subject<AudioClip> _turnRequests = new();
        private System.IDisposable _turnSubscription;
        private double _lastTurnTime = double.NegativeInfinity;
        private AudioSource _turnSource;

        public float MasterVolume { get; private set; }
        public float BGMVolume { get; private set; }
        public float SFXVolume { get; private set; }

        private void Awake()
        {
            Initialize();
            _turnSubscription = _turnRequests.Subscribe(HandleTurnSound);
        }

        private void Start()
        {
            LoadSettings();

            HandlePauseStateChanged(_pauseService != null && _pauseService.IsPaused);
        }

        private void OnDestroy()
        {
            _turnSubscription?.Dispose();
            _turnRequests.Dispose();
            UnbindPauseService();
        }

        /// <summary>
        /// 由 GameLoop 子容器绑定本局暂停服务，避免全局音频对象依赖短生命周期对象。
        /// </summary>
        public void BindPauseService(IGamePauseService pauseService)
        {
            if (ReferenceEquals(_pauseService, pauseService))
            {
                HandlePauseStateChanged(pauseService != null && pauseService.IsPaused);
                return;
            }

            UnbindPauseService();
            _pauseService = pauseService;
            if (_pauseService == null)
                return;

            _pauseService.PauseStateChanged += HandlePauseStateChanged;
            HandlePauseStateChanged(_pauseService.IsPaused);
        }

        /// <summary>仅解除当前 GameLoop 的暂停服务绑定。</summary>
        public void UnbindPauseService(IGamePauseService pauseService)
        {
            if (ReferenceEquals(_pauseService, pauseService))
                UnbindPauseService();
        }

        private void UnbindPauseService()
        {
            if (_pauseService != null)
                _pauseService.PauseStateChanged -= HandlePauseStateChanged;
            _pauseService = null;
        }

        private void HandlePauseStateChanged(bool isPaused)
        {
            if (isPaused)
                OnGamePause();
            else
                OnGameResume();
        }

        private void Initialize()
        {
            InitializeBgmSource();

            for (int i = 0; i < sfxPoolSize; i++)
            {
                AudioSource source = CreateSfxSource();
                sfxSources.Add(source);
                sfxPool.Enqueue(source);
            }

            foreach (var namedClip in sfxClips)
            {
                if (!string.IsNullOrEmpty(namedClip.name) && namedClip.clip != null)
                {
                    sfxDictionary[namedClip.name] = namedClip.clip;
                }
            }

            Debug.Log("音频管理器初始化完成");
        }

        /// <summary>提交实际转动事件；只有首次或超过两秒无转动后才播放声音。</summary>
        /// <param name="clip">本次转动配置的音频。</param>
        public void PlayTurnSFX(AudioClip clip)
        {
            _turnRequests.OnNext(clip);
        }

        /// <summary>每次转动刷新活动时间，并通过共享音源池播放不重叠的首声。</summary>
        /// <param name="clip">本次转动音频；空音频仍记录转动活动。</param>
        private void HandleTurnSound(AudioClip clip)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            double silence = now - _lastTurnTime;
            _lastTurnTime = now;
            if (silence <= TurnSilenceSeconds || clip == null || _turnSource != null)
            {
                return;
            }

            AudioSource source = GetSFXSource();
            if (source == null)
            {
                return;
            }

            _turnSource = source;
            source.transform.position = transform.position;
            PlaySfxSource(source, clip, 1f, 1f, 0f);
        }

        public void PlaySFX(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;

            AudioSource source = GetSFXSource();
            if (source == null) return;

            source.transform.position = transform.position;
            PlaySfxSource(source, clip, volume, pitch, 0f);
        }

        public void PlaySFXByName(string name, float volume = 1f, float pitch = 1f)
        {
            if (sfxDictionary.TryGetValue(name, out AudioClip clip))
            {
                PlaySFX(clip, volume, pitch);
            }
            else
            {
                Debug.LogWarning($"未找到音效: {name}");
            }
        }

        public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;

            AudioSource source = GetSFXSource();
            if (source == null) return;

            source.transform.position = position;
            PlaySfxSource(source, clip, volume, pitch, 1f);
        }

        public void PlayRandomSFX(AudioClip[] clips, float volume = 1f)
        {
            if (clips == null || clips.Length == 0) return;

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            PlaySFX(clip, volume);
        }

        /// <summary>停止所有正在播放的音效，并立即将其音源归还音效池。</summary>
        public void StopAllSFX()
        {
            foreach (AudioSource source in sfxSources)
            {
                if (source == null || !sfxLocalVolumes.ContainsKey(source))
                {
                    continue;
                }

                sfxLeaseIds[source]++;
                RecycleSfxSource(source);
            }
        }

        private AudioSource GetSFXSource()
        {
            while (sfxPool.Count > 0)
            {
                AudioSource source = sfxPool.Dequeue();
                if (source != null)
                {
                    return source;
                }
            }

            if (sfxSources.Count >= Mathf.Max(sfxPoolSize, maximumSfxSourceCount))
                return null;

            AudioSource newSource = CreateSfxSource();
            sfxSources.Add(newSource);
            return newSource;
        }

        private AudioSource CreateSfxSource()
        {
            GameObject sourceObject = new("SFX Source");
            sourceObject.transform.SetParent(transform, false);
            sourceObject.SetActive(false);

            AudioSource source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            sourceObject.SetActive(true);
            return source;
        }

        /// <summary>按主音量、音效分类音量和调用方倍率设置单次音效。</summary>
        private void PlaySfxSource(
            AudioSource source,
            AudioClip clip,
            float volume,
            float pitch,
            float fallbackSpatialBlend)
        {
            ResolveSfxSettings(clip, fallbackSpatialBlend, out float catalogVolume, out float spatialBlend);
            source.Stop();
            source.clip = clip;
            source.spatialBlend = spatialBlend;
            sfxLocalVolumes[source] = volume * catalogVolume;
            source.volume = MasterVolume * SFXVolume * sfxLocalVolumes[source];
            source.pitch = pitch;
            source.Play();

            int leaseId = sfxLeaseIds.TryGetValue(source, out int previousLeaseId)
                ? previousLeaseId + 1
                : 1;
            sfxLeaseIds[source] = leaseId;

            float playbackLength = clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch));
            StartCoroutine(ReturnSFXSource(source, playbackLength, leaseId));
        }

        /// <summary>在音效时长结束且租约仍有效时归还音源。</summary>
        /// <param name="source">正在播放音效的音源。</param>
        /// <param name="delay">本次音效预计播放时长。</param>
        /// <param name="leaseId">本次播放的租约编号。</param>
        /// <returns>音效播放期间等待，之后归还有效音源的协程。</returns>
        private IEnumerator ReturnSFXSource(AudioSource source, float delay, int leaseId)
        {
            yield return new WaitForSecondsRealtime(delay);

            if (source == null ||
                !sfxLeaseIds.TryGetValue(source, out int currentLeaseId) ||
                currentLeaseId != leaseId)
            {
                yield break;
            }

            RecycleSfxSource(source);
        }

        /// <summary>停止音效并复位音源，再将其放回可复用队列。</summary>
        /// <param name="source">已结束或被显式停止的音源。</param>
        private void RecycleSfxSource(AudioSource source)
        {
            if (_turnSource == source)
            {
                _turnSource = null;
            }

            source.Stop();
            source.clip = null;
            source.spatialBlend = 0f;
            source.transform.localPosition = Vector3.zero;
            source.transform.localRotation = Quaternion.identity;
            sfxLocalVolumes.Remove(source);
            sfxPool.Enqueue(source);
        }

        public void SetSFXVolume(float volume)
        {
            SFXVolume = Mathf.Clamp01(volume);

            if (mainMixer != null)
            {
                float db = SFXVolume > 0 ? Mathf.Log10(SFXVolume) * 20 : -80f;
                mainMixer.SetFloat(sfxVolumeParam, db);
            }

            ApplySfxPlaybackVolumes();
        }

        public void ToggleMute()
        {
            if (MasterVolume > 0)
            {
                SetMasterVolume(0);
            }
            else
            {
                SetMasterVolume(defaultVolume);
            }
        }

        /// <summary>根据最新主音量与 SFX 音量更新所有正在播放的音效。</summary>
        private void ApplySfxPlaybackVolumes()
        {
            foreach (KeyValuePair<AudioSource, float> entry in sfxLocalVolumes)
            {
                if (entry.Key != null)
                {
                    entry.Key.volume = MasterVolume * SFXVolume * entry.Value;
                }
            }
        }

        public void SaveSettings()
        {
            var settings = SaveSystem.LoadSettings();
            settings.masterVolume = MasterVolume;
            settings.bgmVolume = BGMVolume;
            settings.sfxVolume = SFXVolume;
            SaveSystem.SaveSettings(settings);
        }

        public void LoadSettings()
        {
            var settings = SaveSystem.LoadSettings();
            SetMasterVolume(settings.masterVolume);
            SetBGMVolume(settings.bgmVolume);
            SetSFXVolume(settings.sfxVolume);
        }
    }

    [System.Serializable]
    public class NamedAudioClip
    {
        public string name;
        public AudioClip clip;
    }
}
