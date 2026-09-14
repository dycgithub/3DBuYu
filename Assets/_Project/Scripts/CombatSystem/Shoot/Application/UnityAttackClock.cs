namespace CombatSystem
{
    /// <summary>使用 Unity 缩放时间驱动攻击冷却。</summary>
    public sealed class UnityAttackClock : IAttackClock
    {
        /// <summary>返回当前 Unity 缩放时间。</summary>
        public float CurrentTimeSeconds => UnityEngine.Time.time;
    }
}
