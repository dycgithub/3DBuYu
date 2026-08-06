using System;

namespace Services
{
    /// <summary>
    /// 游戏事件服务接口。
    /// 替代直接调用 <see cref="GameSystem.GameManager.Instance"/> 的生命周期方法，
    /// 使 EnemyBase 等通过事件而非单例通信。
    /// </summary>
    public interface IGameEventService
    {
        /// <summary>敌人被击杀时触发（由 EnemyBase.OnDeath 调用）。参数为积分值。</summary>
        event Action<int> EnemyKilled;

        /// <summary>通知敌人被击杀。</summary>
        /// <param name="pointsValue">该敌人提供的积分。</param>
        void NotifyEnemyKilled(int pointsValue);
    }
}
