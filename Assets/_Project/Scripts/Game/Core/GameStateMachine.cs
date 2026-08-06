using System;

namespace GameSystem
{
    /// <summary>
    /// 游戏状态机 — 纯逻辑，可单元测试。
    /// 负责 <see cref="GameState"/> 的转换与变更通知，
    /// 不关心具体业务（积分、UI、暂停）— 由 GameManager 订阅 <see cref="OnStateChanged"/> 后处理。
    /// </summary>
    public class GameStateMachine
    {
        /// <summary>当前游戏状态。</summary>
        public GameState CurrentState { get; private set; } = GameState.Menu;

        /// <summary>状态变化事件（旧状态, 新状态）。</summary>
        public event Action<GameState, GameState> OnStateChanged;

        /// <summary>
        /// 转换到新状态。相同状态直接返回，避免冗余事件。
        /// </summary>
        public void ChangeState(GameState newState)
        {
            if (CurrentState == newState) return;
            var old = CurrentState;
            CurrentState = newState;
            OnStateChanged?.Invoke(old, newState);
        }
    }
}
