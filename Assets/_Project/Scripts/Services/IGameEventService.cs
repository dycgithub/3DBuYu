using System;

namespace Services
{
    /// <summary>
    /// 游戏事件服务接口。
    /// 敌人通过事件而非单例通信，游戏管理器负责返回本局实际计入的奖励。
    /// </summary>
    public interface IGameEventService
    {
        /// <summary>敌人被击杀时触发，参数为本局实际计入的积分值。</summary>
        event Action<int> EnemyKilled;

        /// <summary>通知敌人被击杀，并返回本局实际计入的积分。</summary>
        /// <param name="pointsValue">该敌人提供的积分。</param>
        int NotifyEnemyKilled(int pointsValue);
    }
}
