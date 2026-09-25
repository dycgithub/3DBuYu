namespace CombatSystem
{
    /// <summary>技能激活失败原因。</summary>
    public enum SkillActivationFailureReason
    {
        None,
        SkillNotFound,
        CombatActionsNotAllowed,
        CooldownActive,
        UsesExhausted,
        SourceItemUnavailable,
        InvalidTarget,
        NoValidTarget,
        InsufficientEnergy,
        ActionFailed,
        SourceConsumptionFailed
    }
}
