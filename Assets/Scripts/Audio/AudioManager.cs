using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace GameSystem
{
    /// <summary>
    /// 背景音乐类型
    /// </summary>
    public enum BGMType
    {
        None,
        Menu,       // 主菜单
        Game,       // 游戏
        Battle,     // 战斗
        Victory,    // 胜利
        GameOver    // 失败
    }

    /// <summary>
    /// 音频管理器
    /// 管理游戏中的所有音频播放
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        [Header("音频混音器")]
        [Tooltip("主混音器")]
        public AudioMixer mainMixer;

        [Tooltip("主音量参数名")]
        public string masterVolumeParam = "MasterVolume";

        [Tooltip("BGM音量参数名")]
        public string bgmVolumeParam = "BGMVolume";

        [Tooltip("SFX音量参数名")]
        public string sfxVolumeParam = "SFXVolume";

        [Header("背景音乐")]
        [Tooltip("菜单音乐")]
        public AudioClip menuBGM;

        [Tooltip("游戏音乐")]
        public AudioClip gameBGM;

        [Tooltip("战斗音乐")]
        public AudioClip battleBGM;

        [Tooltip("胜利音乐")]
        public AudioClip victoryBGM;

        [Tooltip("失败音乐")]
        public AudioClip gameOverBGM;

        [Tooltip("背景音乐切换淡入时间")]
        public float bgmFadeInTime = 2f;

        [Tooltip("背景音乐切换淡出时间")]
        public float bgmFadeOutTime = 1f;

        [Header("音效")]
        [Tooltip("音效池大小")]
        public int sfxPoolSize = 10;

        [Tooltip("音效预设列表")]
        public List<NamedAudioClip> sfxClips = new List<NamedAudioClip>();

        [Header("设置")]
        [Tooltip("默认音量")]
        [Range(0f, 1f)]
        public float defaultVolume = 0.8f;

        [Tooltip("暂停时是否降低音量")]
        public bool lowerVolumeOnPause = true;

        [Tooltip("暂停时音量倍数")]
        [Range(0f, 1f)]
        public float pauseVolumeMultiplier = 0.3f;

        // 单例
        public static AudioManager Instance { get; private set; }

        // 音频源
        private AudioSource bgmSource;
        private List<AudioSource> sfxSources = new List<AudioSource>();
        private Queue<AudioSource> sfxPool = new Queue<AudioSource>();

        // 状态
        private BGMType currentBGM = BGMType.None;
        private float currentBGMVolume = 1f;
        private Dictionary<string, AudioClip> sfxDictionary = new Dictionary<string, AudioClip>();

        #region 属性

        public float MasterVolume { get; private set; }
        public float BGMVolume { get; private set; }
        public float SFXVolume { get; private set; }

        #endregion

        #region Unity生命周期

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            Initialize();
        }

        private void Start()
        {
            LoadSettings();
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化音频系统
        /// </summary>
        private void Initialize()
        {
            // 创建BGM音频源
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;

            // 创建SFX音频源池
            for (int i = 0; i < sfxPoolSize; i++)
            {
                AudioSource source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                sfxSources.Add(source);
                sfxPool.Enqueue(source);
            }

            // 构建音效字典
            foreach (var namedClip in sfxClips)
            {
                if (!string.IsNullOrEmpty(namedClip.name) && namedClip.clip != null)
                {
                    sfxDictionary[namedClip.name] = namedClip.clip;
                }
            }

            Debug.Log("音频管理器初始化完成");
        }

        #endregion

        #region 背景音乐

        /// <summary>
        /// 播放背景音乐
        /// </summary>
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

        /// <summary>
        /// 停止背景音乐
        /// </summary>
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

        /// <summary>
        /// 暂停背景音乐
        /// </summary>
        public void PauseBGM()
        {
            bgmSource.Pause();
        }

        /// <summary>
        /// 恢复背景音乐
        /// </summary>
        public void ResumeBGM()
        {
            bgmSource.UnPause();
        }

        /// <summary>
        /// 获取背景音乐片段
        /// </summary>
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

        /// <summary>
        /// 背景音乐淡入淡出协程
        /// </summary>
        private System.Collections.IEnumerator FadeBGM(AudioClip newClip)
        {
            // 淡出
            float startVolume = bgmSource.volume;
            float elapsed = 0f;

            while (elapsed < bgmFadeOutTime)
            {
                elapsed += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / bgmFadeOutTime);
                yield return null;
            }

            // 切换
            bgmSource.Stop();
            bgmSource.clip = newClip;
            bgmSource.Play();

            // 淡入
            elapsed = 0f;
            while (elapsed < bgmFadeInTime)
            {
                elapsed += Time.deltaTime;
                bgmSource.volume = Mathf.Lerp(0f, BGMVolume, elapsed / bgmFadeInTime);
                yield return null;
            }

            bgmSource.volume = BGMVolume;
        }

        /// <summary>
        /// 背景音乐淡出协程
        /// </summary>
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

        #endregion

        #region 音效播放

        /// <summary>
        /// 播放音效
        /// </summary>
        public void PlaySFX(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (clip == null) return;

            AudioSource source = GetSFXSource();
            if (source == null) return;

            source.clip = clip;
            source.volume = SFXVolume * volume;
            source.pitch = pitch;
            source.Play();

            // 播放结束后归还到池
            StartCoroutine(ReturnSFXSource(source, clip.length));
        }

        /// <summary>
        /// 通过名称播放音效
        /// </summary>
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

        /// <summary>
        /// 在指定位置播放音效
        /// </summary>
        public void PlaySFXAtPosition(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, position, SFXVolume * volume);
        }

        /// <summary>
        /// 随机播放多个音效中的一个
        /// </summary>
        public void PlayRandomSFX(AudioClip[] clips, float volume = 1f)
        {
            if (clips == null || clips.Length == 0) return;

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            PlaySFX(clip, volume);
        }

        /// <summary>
        /// 获取可用的音效音频源
        /// </summary>
        private AudioSource GetSFXSource()
        {
            if (sfxPool.Count > 0)
            {
                return sfxPool.Dequeue();
            }

            // 如果没有可用的，找一个已停止的
            foreach (var source in sfxSources)
            {
                if (!source.isPlaying)
                {
                    return source;
                }
            }

            // 仍然没有，创建新的
            AudioSource newSource = gameObject.AddComponent<AudioSource>();
            newSource.playOnAwake = false;
            sfxSources.Add(newSource);
            return newSource;
        }

        /// <summary>
        /// 归还音效音频源到池
        /// </summary>
        private System.Collections.IEnumerator ReturnSFXSource(AudioSource source, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (!sfxPool.Contains(source))
            {
                sfxPool.Enqueue(source);
            }
        }

        #endregion

        #region 音量控制

        /// <summary>
        /// 设置主音量
        /// </summary>
        public void SetMasterVolume(float volume)
        {
            MasterVolume = Mathf.Clamp01(volume);

            if (mainMixer != null)
            {
                // 转换为分贝
                float db = MasterVolume > 0 ? Mathf.Log10(MasterVolume) * 20 : -80f;
                mainMixer.SetFloat(masterVolumeParam, db);
            }

            bgmSource.volume = MasterVolume * BGMVolume;
        }

        /// <summary>
        /// 设置BGM音量
        /// </summary>
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

        /// <summary>
        /// 设置SFX音量
        /// </summary>
        public void SetSFXVolume(float volume)
        {
            SFXVolume = Mathf.Clamp01(volume);

            if (mainMixer != null)
            {
                float db = SFXVolume > 0 ? Mathf.Log10(SFXVolume) * 20 : -80f;
                mainMixer.SetFloat(sfxVolumeParam, db);
            }
        }

        /// <summary>
        /// 静音切换
        /// </summary>
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

        /// <summary>
        /// 处理游戏暂停
        /// </summary>
        public void OnGamePause()
        {
            if (lowerVolumeOnPause)
            {
                bgmSource.volume = BGMVolume * MasterVolume * pauseVolumeMultiplier;
            }
        }

        /// <summary>
        /// 处理游戏恢复
        /// </summary>
        public void OnGameResume()
        {
            bgmSource.volume = BGMVolume * MasterVolume;
        }

        #endregion

        #region 存档/读档

        /// <summary>
        /// 保存设置
        /// </summary>
        public void SaveSettings()
        {
            var settings = SaveSystem.LoadSettings();
            settings.masterVolume = MasterVolume;
            settings.bgmVolume = BGMVolume;
            settings.sfxVolume = SFXVolume;
            SaveSystem.SaveSettings(settings);
        }

        /// <summary>
        /// 加载设置
        /// </summary>
        public void LoadSettings()
        {
            var settings = SaveSystem.LoadSettings();
            SetMasterVolume(settings.masterVolume);
            SetBGMVolume(settings.bgmVolume);
            SetSFXVolume(settings.sfxVolume);
        }

        #endregion
    }

    /// <summary>
    /// 命名音频片段
    /// </summary>
    [System.Serializable]
    public class NamedAudioClip
    {
        [Tooltip("音效名称")]
        public string name;

        [Tooltip("音频片段")]
        public AudioClip clip;
    }
}
