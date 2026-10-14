using System.Collections;
using System.Collections.Generic;
using Services;
using UnityEngine;
using UnityEngine.Audio;

namespace GameSystem
{
    public enum BGMType
    {
        None,
        Menu,
        Game,
        Battle,
        Victory,
        GameOver
    }

    public class AudioManager : MonoBehaviour
    {
        [Header("音频混音器")]
        public AudioMixer mainMixer;
        public string masterVolumeParam = "MasterVolume";
        public string bgmVolumeParam = "BGMVolume";
        public string sfxVolumeParam = "SFXVolume";

        [Header("背景音乐")]
        public AudioClip menuBGM;
        public AudioClip gameBGM;
        public AudioClip battleBGM;
        public AudioClip victoryBGM;
        public AudioClip gameOverBGM;
        public float bgmFadeInTime = 2f;
        public float bgmFadeOutTime = 1f;

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

        private AudioSource bgmSource;
        private readonly List<AudioSource> sfxSources = new();
        private readonly Queue<AudioSource> sfxPool = new();
        private readonly Dictionary<AudioSource, int> sfxLeaseIds = new();
        private readonly Dictionary<AudioSource, float> sfxLocalVolumes = new();
        private Coroutine bgmTransition;
        private BGMType currentBGM = BGMType.None;
        private readonly Dictionary<string, AudioClip> sfxDictionary = new();

        public float MasterVolume { get; private set; }
        public float BGMVolume { get; private set; }
        public float SFXVolume { get; private set; }

        private void Awake()
        {
            Initialize();
        }

        private void Start()
        {
            LoadSettings();

            HandlePauseStateChanged(_pauseService != null && _pauseService.IsPaused);
        }

        private void OnDestroy()
        {
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
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;

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

        /// <summary>播放指定背景音乐；相同曲目的重复请求不重启播放。</summary>
        /// <param name="type">背景音乐类型。</param>
        /// <param name="fade">是否在切换时淡变。</param>
        public void PlayBGM(BGMType type, bool fade = true)
        {
            if (currentBGM == type)
            {
                return;
            }

            AudioClip clip = GetBGMClip(type);
            if (clip == null)
            {
                return;
            }

            ApplyBgmRequest(type, clip, fade);
        }

        /// <summary>停止背景音乐，并取消此前尚未完成的淡变。</summary>
        /// <param name="fade">是否在停止前淡出。</param>
        public void StopBGM(bool fade = true)
        {
            ApplyBgmRequest(BGMType.None, null, fade);
        }

        /// <summary>取消旧淡变后应用最新的 BGM 状态请求。</summary>
        /// <param name="type">请求后的背景音乐状态。</param>
        /// <param name="clip">目标曲目；为空表示停止播放。</param>
        /// <param name="fade">是否执行淡变。</param>
        private void ApplyBgmRequest(BGMType type, AudioClip clip, bool fade)
        {
            StopBgmTransition();
            currentBGM = type;

            if (fade)
            {
                bgmTransition = StartCoroutine(FadeBGM(clip));
                return;
            }

            if (clip == null)
            {
                bgmSource.Stop();
                return;
            }

            bgmSource.clip = clip;
            ApplyBgmPlaybackVolume();
            bgmSource.Play();
        }

        /// <summary>停止当前淡变，避免旧协程覆盖最新请求。</summary>
        private void StopBgmTransition()
        {
            if (bgmTransition != null)
            {
                StopCoroutine(bgmTransition);
                bgmTransition = null;
            }
        }

        public void PauseBGM()
        {
            bgmSource.Pause();
        }

        public void ResumeBGM()
        {
            bgmSource.UnPause();
        }

        private AudioClip GetBGMClip(BGMType type)
        {
            return type switch
            {
                BGMType.Menu => menuBGM,
                BGMType.Game => gameBGM,
                BGMType.Battle => battleBGM,
                BGMType.Victory => victoryBGM,
                BGMType.GameOver => gameOverBGM,
                _ => null
            };
        }

        /// <summary>将当前曲目淡出后切换、淡入目标曲目；空目标表示淡出停止。</summary>
        /// <param name="targetClip">目标曲目；为空时淡出后停止。</param>
        /// <returns>逐帧执行的 BGM 淡变。</returns>
        private IEnumerator FadeBGM(AudioClip targetClip)
        {
            float startVolume = bgmSource.volume;
            float elapsed = 0f;

            while (elapsed < bgmFadeOutTime)
            {
                elapsed += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / bgmFadeOutTime);
                yield return null;
            }

            if (targetClip == null)
            {
                bgmSource.Stop();
                yield break;
            }

            bgmSource.Stop();
            bgmSource.clip = targetClip;
            bgmSource.Play();

            elapsed = 0f;
            while (elapsed < bgmFadeInTime)
            {
                elapsed += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(0f, GetBgmPlaybackVolume(), elapsed / bgmFadeInTime);
                yield return null;
            }

            ApplyBgmPlaybackVolume();
        }

        public void PlaySFX(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;

            AudioSource source = GetSFXSource();
            if (source == null) return;

            source.transform.position = transform.position;
            source.spatialBlend = 0f;
            PlaySfxSource(source, clip, volume, pitch);
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
            source.spatialBlend = 1f;
            PlaySfxSource(source, clip, volume, pitch);
        }

        public void PlayRandomSFX(AudioClip[] clips, float volume = 1f)
        {
            if (clips == null || clips.Length == 0) return;

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            PlaySFX(clip, volume);
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
        private void PlaySfxSource(AudioSource source, AudioClip clip, float volume, float pitch)
        {
            source.Stop();
            source.clip = clip;
            sfxLocalVolumes[source] = volume;
            source.volume = MasterVolume * SFXVolume * volume;
            source.pitch = pitch;
            source.Play();

            int leaseId = sfxLeaseIds.TryGetValue(source, out int previousLeaseId)
                ? previousLeaseId + 1
                : 1;
            sfxLeaseIds[source] = leaseId;

            float playbackLength = clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch));
            StartCoroutine(ReturnSFXSource(source, playbackLength, leaseId));
        }

        private IEnumerator ReturnSFXSource(AudioSource source, float delay, int leaseId)
        {
            yield return new WaitForSecondsRealtime(delay);

            if (source == null ||
                !sfxLeaseIds.TryGetValue(source, out int currentLeaseId) ||
                currentLeaseId != leaseId)
            {
                yield break;
            }

            source.Stop();
            source.clip = null;
            source.spatialBlend = 0f;
            source.transform.localPosition = Vector3.zero;
            source.transform.localRotation = Quaternion.identity;
            sfxLocalVolumes.Remove(source);
            sfxPool.Enqueue(source);
        }

        public void SetMasterVolume(float volume)
        {
            MasterVolume = Mathf.Clamp01(volume);

            if (mainMixer != null)
            {
                float db = MasterVolume > 0 ? Mathf.Log10(MasterVolume) * 20 : -80f;
                mainMixer.SetFloat(masterVolumeParam, db);
            }

            ApplyBgmPlaybackVolume();
            ApplySfxPlaybackVolumes();
        }

        public void SetBGMVolume(float volume)
        {
            BGMVolume = Mathf.Clamp01(volume);

            if (mainMixer != null)
            {
                float db = BGMVolume > 0 ? Mathf.Log10(BGMVolume) * 20 : -80f;
                mainMixer.SetFloat(bgmVolumeParam, db);
            }

            ApplyBgmPlaybackVolume();
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

        public void OnGamePause()
        {
            ApplyBgmPlaybackVolume();
        }

        public void OnGameResume()
        {
            ApplyBgmPlaybackVolume();
        }

        private void ApplyBgmPlaybackVolume()
        {
            if (bgmSource != null)
                bgmSource.volume = GetBgmPlaybackVolume();
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

        private float GetBgmPlaybackVolume()
        {
            float volume = MasterVolume * BGMVolume;
            if (_pauseService != null && _pauseService.IsPaused && lowerVolumeOnPause)
                volume *= pauseVolumeMultiplier;

            return volume;
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
