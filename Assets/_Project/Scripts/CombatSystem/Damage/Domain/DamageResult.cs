namespace CombatSystem
{
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
