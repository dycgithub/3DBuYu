using System.Collections.Generic;
using Services;
using UnityEngine;
using UnityEngine.Audio;
using VContainer;

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

        [Inject] private IGamePauseService _pauseService;

        private AudioSource bgmSource;
        private readonly List<AudioSource> sfxSources = new();
        private readonly Queue<AudioSource> sfxPool = new();
        private readonly Dictionary<AudioSource, int> sfxLeaseIds = new();
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

            if (_pauseService != null)
            {
                _pauseService.PauseStateChanged += HandlePauseStateChanged;
                HandlePauseStateChanged(_pauseService.IsPaused);
            }
        }

        private void OnDestroy()
        {
            if (_pauseService != null)
                _pauseService.PauseStateChanged -= HandlePauseStateChanged;
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

        public void PlayBGM(BGMType type, bool fade = true)
        {
            if (currentBGM == type) return;

            AudioClip clip = GetBGMClip(type);
            if (clip == null) return;

            currentBGM = type;

            if (fade)
            {
                StartCoroutine(FadeBGM(clip));
            }
            else
            {
                bgmSource.clip = clip;
                ApplyBgmPlaybackVolume();
                bgmSource.Play();
            }
        }

        public void StopBGM(bool fade = true)
        {
            currentBGM = BGMType.None;

            if (fade)
            {
                StartCoroutine(FadeOutBGM());
            }
            else
            {
                bgmSource.Stop();
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

        private System.Collections.IEnumerator FadeBGM(AudioClip newClip)
        {
            float startVolume = bgmSource.volume;
            float elapsed = 0f;

            while (elapsed < bgmFadeOutTime)
            {
                elapsed += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / bgmFadeOutTime);
                yield return null;
            }

            bgmSource.Stop();
            bgmSource.clip = newClip;
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

        private System.Collections.IEnumerator FadeOutBGM()
        {
            float startVolume = bgmSource.volume;
            float elapsed = 0f;

            while (elapsed < bgmFadeOutTime)
            {
                elapsed += Time.unscaledDeltaTime;
                bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / bgmFadeOutTime);
                yield return null;
            }

            bgmSource.Stop();
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
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            return source;
        }

        private void PlaySfxSource(AudioSource source, AudioClip clip, float volume, float pitch)
        {
            source.Stop();
            source.clip = clip;
            source.volume = SFXVolume * volume;
            source.pitch = pitch;
            source.Play();

            int leaseId = sfxLeaseIds.TryGetValue(source, out int previousLeaseId)
                ? previousLeaseId + 1
                : 1;
            sfxLeaseIds[source] = leaseId;

            float playbackLength = clip.length / Mathf.Max(0.01f, Mathf.Abs(pitch));
            StartCoroutine(ReturnSFXSource(source, playbackLength, leaseId));
        }

        private System.Collections.IEnumerator ReturnSFXSource(AudioSource source, float delay, int leaseId)
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
