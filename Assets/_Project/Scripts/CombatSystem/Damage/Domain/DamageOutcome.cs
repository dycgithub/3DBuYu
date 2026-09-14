namespace CombatSystem
{
    /// <summary>
    /// 表示伤害请求在目标上的最终处理状态。
    /// </summary>
    public enum DamageOutcome
    {
        /// <summary>目标或请求无法处理；当前管线主要用于表示目标无效。</summary>
        Invalid = 0,

        /// <summary>目标完全闪避了伤害。</summary>
        Dodged = 1,

        /// <summary>请求被处理，但没有扣除生命值。</summary>
        Blocked = 2,

        /// <summary>扣除生命值，但目标仍存活。</summary>
        Applied = 3,

        /// <summary>扣除生命值并使目标死亡。</summary>
        Killed = 4
    }
}
