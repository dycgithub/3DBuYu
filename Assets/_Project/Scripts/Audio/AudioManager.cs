using System.Collections.Generic;
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
        public List<NamedAudioClip> sfxClips = new List<NamedAudioClip>();

        [Header("设置")]
        [Range(0f, 1f)]
        public float defaultVolume = 0.8f;
        public bool lowerVolumeOnPause = true;
        [Range(0f, 1f)]
        public float pauseVolumeMultiplier = 0.3f;

        private AudioSource bgmSource;
        private List<AudioSource> sfxSources = new List<AudioSource>();
        private Queue<AudioSource> sfxPool = new Queue<AudioSource>();
        private BGMType currentBGM = BGMType.None;
        private Dictionary<string, AudioClip> sfxDictionary = new Dictionary<string, AudioClip>();

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
        }

        private void Initialize()
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;

            for (int i = 0; i < sfxPoolSize; i++)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
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
                bgmSource.volume = BGMVolume;
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
                elapsed += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / bgmFadeOutTime);
                yield return null;
            }

            bgmSource.Stop();
            bgmSource.clip = newClip;
            bgmSource.Play();

            elapsed = 0f;
            while (elapsed < bgmFadeInTime)
            {
                elapsed += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(0f, BGMVolume, elapsed / bgmFadeInTime);
                yield return null;
            }

            bgmSource.volume = BGMVolume;
        }

        private System.Collections.IEnumerator FadeOutBGM()
        {
            float startVolume = bgmSource.volume;
            float elapsed = 0f;

            while (elapsed < bgmFadeOutTime)
            {
                elapsed += Time.deltaTime;
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

            source.clip = clip;
            source.volume = SFXVolume * volume;
            source.pitch = pitch;
            source.Play();

            StartCoroutine(ReturnSFXSource(source, clip.length));
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

        public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, position, SFXVolume * volume);
        }

        public void PlayRandomSFX(AudioClip[] clips, float volume = 1f)
        {
            if (clips == null || clips.Length == 0) return;

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            PlaySFX(clip, volume);
        }

        private AudioSource GetSFXSource()
        {
            if (sfxPool.Count > 0)
            {
                return sfxPool.Dequeue();
            }

            foreach (var source in sfxSources)
            {
                if (!source.isPlaying)
                {
                    return source;
                }
            }

            AudioSource newSource = gameObject.AddComponent<AudioSource>();
            newSource.playOnAwake = false;
            sfxSources.Add(newSource);
            return newSource;
        }

        private System.Collections.IEnumerator ReturnSFXSource(AudioSource source, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (!sfxPool.Contains(source))
            {
                sfxPool.Enqueue(source);
            }
        }

        public void SetMasterVolume(float volume)
        {
            MasterVolume = Mathf.Clamp01(volume);

            if (mainMixer != null)
            {
                float db = MasterVolume > 0 ? Mathf.Log10(MasterVolume) * 20 : -80f;
                mainMixer.SetFloat(masterVolumeParam, db);
            }

            bgmSource.volume = MasterVolume * BGMVolume;
        }

        public void SetBGMVolume(float volume)
        {
            BGMVolume = Mathf.Clamp01(volume);

            if (mainMixer != null)
            {
                float db = BGMVolume > 0 ? Mathf.Log10(BGMVolume) * 20 : -80f;
                mainMixer.SetFloat(bgmVolumeParam, db);
            }

            bgmSource.volume = MasterVolume * BGMVolume;
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
            if (lowerVolumeOnPause)
            {
                bgmSource.volume = BGMVolume * MasterVolume * pauseVolumeMultiplier;
            }
        }

        public void OnGameResume()
        {
            bgmSource.volume = BGMVolume * MasterVolume;
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
