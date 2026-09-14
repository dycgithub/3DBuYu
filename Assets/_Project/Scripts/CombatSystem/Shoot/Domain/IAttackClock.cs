namespace CombatSystem
{
    /// <summary>提供攻击冷却所需的时间来源。</summary>
    public interface IAttackClock
    {
        /// <summary>当前时间，单位为秒。</summary>
        float CurrentTimeSeconds { get; }
    }
}
