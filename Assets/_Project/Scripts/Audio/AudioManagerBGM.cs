using System.Collections;
using UnityEngine;

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

    /// <summary>AudioManager 的背景音乐目录查询、淡变与音量计算。</summary>
    public partial class AudioManager
    {
        [Header("背景音乐")]
        [SerializeField] private MusicCatalogSO _musicCatalog;
        public float bgmFadeInTime = 2f;
        public float bgmFadeOutTime = 1f;

        private AudioSource bgmSource;
        private Coroutine bgmTransition;
        private BGMType currentBGM = BGMType.None;
        private AudioClip _requestedBgmClip;
        private float _activeBgmTrackVolume = 1f;
        private float _bgmFadeMultiplier = 1f;

        /// <summary>创建背景音乐音源。</summary>
        private void InitializeBgmSource()
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
        }

        /// <summary>播放指定背景音乐；同曲请求不重启，只更新曲目音量。</summary>
        /// <param name="type">背景音乐类型。</param>
        /// <param name="fade">是否在切换时淡变。</param>
        public void PlayBGM(BGMType type, bool fade = true)
        {
            if (_musicCatalog == null ||
                !_musicCatalog.TryGet(type, out MusicCatalogEntry entry) ||
                entry.Clip == null)
            {
                return;
            }

            float trackVolume = Mathf.Clamp01(entry.Volume);
            if (currentBGM == type)
            {
                if (_requestedBgmClip == entry.Clip)
                {
                    if (bgmSource.clip == entry.Clip)
                    {
                        _activeBgmTrackVolume = trackVolume;
                        ApplyBgmPlaybackVolume();
                    }

                    return;
                }

                ApplyBgmRequest(type, entry.Clip, trackVolume, fade);
                return;
            }

            ApplyBgmRequest(type, entry.Clip, trackVolume, fade);
        }

        /// <summary>停止背景音乐，并取消此前尚未完成的淡变。</summary>
        /// <param name="fade">是否在停止前淡出。</param>
        public void StopBGM(bool fade = true)
        {
            ApplyBgmRequest(BGMType.None, null, 1f, fade);
        }

        /// <summary>取消旧淡变后应用最新的 BGM 状态请求。</summary>
        /// <param name="type">请求后的背景音乐状态。</param>
        /// <param name="clip">目标曲目；为空表示停止播放。</param>
        /// <param name="trackVolume">目标曲目音量。</param>
        /// <param name="fade">是否执行淡变。</param>
        private void ApplyBgmRequest(BGMType type, AudioClip clip, float trackVolume, bool fade)
        {
            StopBgmTransition();
            currentBGM = type;
            _requestedBgmClip = clip;

            if (fade)
            {
                bgmTransition = StartCoroutine(FadeBGM(clip, trackVolume));
                return;
            }

            if (clip == null)
            {
                bgmSource.Stop();
                _bgmFadeMultiplier = 1f;
                ApplyBgmPlaybackVolume();
                return;
            }

            bgmSource.clip = clip;
            _activeBgmTrackVolume = trackVolume;
            _bgmFadeMultiplier = 1f;
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

        /// <summary>暂停当前背景音乐音源。</summary>
        public void PauseBGM()
        {
            bgmSource.Pause();
        }

        /// <summary>恢复当前背景音乐音源。</summary>
        public void ResumeBGM()
        {
            bgmSource.UnPause();
        }

        /// <summary>淡出当前曲目，再淡入目标曲目；空目标表示停止。</summary>
        /// <param name="targetClip">目标曲目；为空时淡出后停止。</param>
        /// <param name="targetTrackVolume">目标曲目音量。</param>
        /// <returns>逐帧执行的 BGM 淡变。</returns>
        private IEnumerator FadeBGM(AudioClip targetClip, float targetTrackVolume)
        {
            float startMultiplier = _bgmFadeMultiplier;
            float elapsed = 0f;

            while (elapsed < bgmFadeOutTime)
            {
                elapsed += Time.unscaledDeltaTime;
                _bgmFadeMultiplier = Mathf.Lerp(startMultiplier, 0f, elapsed / bgmFadeOutTime);
                ApplyBgmPlaybackVolume();
                yield return null;
            }

            if (targetClip == null)
            {
                bgmSource.Stop();
                _bgmFadeMultiplier = 1f;
                ApplyBgmPlaybackVolume();
                bgmTransition = null;
                yield break;
            }

            bgmSource.Stop();
            bgmSource.clip = targetClip;
            _activeBgmTrackVolume = targetTrackVolume;
            _bgmFadeMultiplier = 0f;
            ApplyBgmPlaybackVolume();
            bgmSource.Play();

            elapsed = 0f;
            while (elapsed < bgmFadeInTime)
            {
                elapsed += Time.unscaledDeltaTime;
                _bgmFadeMultiplier = Mathf.Lerp(0f, 1f, elapsed / bgmFadeInTime);
                ApplyBgmPlaybackVolume();
                yield return null;
            }

            _bgmFadeMultiplier = 1f;
            ApplyBgmPlaybackVolume();
            bgmTransition = null;
        }

        /// <summary>设置主音量并重算 BGM、SFX 的最终播放音量。</summary>
        /// <param name="volume">主音量。</param>
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

        /// <summary>设置 BGM 分类音量并更新当前曲目。</summary>
        /// <param name="volume">BGM 音量。</param>
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

        /// <summary>暂停状态变化时按最新音量倍率更新当前曲目。</summary>
        public void OnGamePause()
        {
            ApplyBgmPlaybackVolume();
        }

        /// <summary>恢复状态变化时按最新音量倍率更新当前曲目。</summary>
        public void OnGameResume()
        {
            ApplyBgmPlaybackVolume();
        }

        /// <summary>应用主音量、BGM 音量、曲目音量、暂停倍率和淡变倍率。</summary>
        private void ApplyBgmPlaybackVolume()
        {
            if (bgmSource != null)
            {
                bgmSource.volume = GetBgmPlaybackVolume() * _bgmFadeMultiplier;
            }
        }

        /// <summary>计算不含淡变倍率的当前 BGM 目标音量。</summary>
        /// <returns>当前曲目的目标播放音量。</returns>
        private float GetBgmPlaybackVolume()
        {
            float volume = MasterVolume * BGMVolume * _activeBgmTrackVolume;
            if (_pauseService != null && _pauseService.IsPaused && lowerVolumeOnPause)
            {
                volume *= pauseVolumeMultiplier;
            }

            return volume;
        }
    }
}
