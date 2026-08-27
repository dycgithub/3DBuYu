namespace CombatSystem
{
    /// <summary>一次伤害请求最终落入的状态。</summary>
    public enum DamageOutcome
    {
        Invalid,
        Dodged,
        Blocked,
        Applied,
        Killed
    }
}
