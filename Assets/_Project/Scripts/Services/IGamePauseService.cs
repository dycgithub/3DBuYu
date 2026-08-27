using System;

namespace Services
{
    /// <summary>
    /// 游戏循环的暂停所有者。只有该服务可以修改 Unity 的全局时标。
    /// </summary>
    public interface IGamePauseService
    {
        bool IsPaused { get; }

        event Action<bool> PauseStateChanged;

        void Pause();
        void Resume();
    }
}
