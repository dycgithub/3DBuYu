using System;
using Services;
using UnityEngine;

namespace GameSystem
{
    /// <summary>
    /// 管理本游戏循环对 Unity 全局时标的暂停请求。
    /// 本局倒计时等规则不应直接修改 Time.timeScale。
    /// </summary>
    public sealed class GamePauseService : IGamePauseService, IDisposable
    {
        private float _timeScaleBeforePause = 1f;

        public bool IsPaused { get; private set; }

        /// <summary>暂停状态发生变化时触发。</summary>
        public event Action<bool> PauseStateChanged;

        /// <summary>
        /// 保存当前时间倍率并暂停 Unity 全局时间。
        /// </summary>
        public void Pause()
        {
            if (IsPaused)
            {
                return;
            }

            _timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
            IsPaused = true;
            PauseStateChanged?.Invoke(true);
        }

        /// <summary>
        /// 恢复暂停前保存的 Unity 全局时间倍率。
        /// </summary>
        public void Resume()
        {
            if (!IsPaused)
            {
                return;
            }

            Time.timeScale = _timeScaleBeforePause;
            IsPaused = false;
            PauseStateChanged?.Invoke(false);
        }

        /// <summary>
        /// 释放服务，并确保 Unity 全局时间倍率已恢复。
        /// </summary>
        public void Dispose()
        {
            Resume();
        }
    }
}
