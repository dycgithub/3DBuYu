namespace CombatSystem
{
    /// <summary>伤害请求经过目标防御后产生的结果。</summary>
    public struct DamageResult
    {
        public DamageOutcome Outcome;
        public float ActualDamage;
        public float RemainingHealth;
        public bool IsKill;

        public bool WasApplied => Outcome == DamageOutcome.Applied || Outcome == DamageOutcome.Killed;
        public bool WasDodged => Outcome == DamageOutcome.Dodged;
    }
}
