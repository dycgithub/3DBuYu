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

        public event Action<bool> PauseStateChanged;

        public void Pause()
        {
            if (IsPaused)
                return;

            _timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
            IsPaused = true;
            PauseStateChanged?.Invoke(true);
        }

        public void Resume()
        {
            if (!IsPaused)
                return;

            Time.timeScale = _timeScaleBeforePause;
            IsPaused = false;
            PauseStateChanged?.Invoke(false);
        }

        public void Dispose()
        {
            Resume();
        }
    }
}
