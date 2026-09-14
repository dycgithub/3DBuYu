namespace CombatSystem
{
    /// <summary>一次技能激活的统一结果。</summary>
    public sealed class SkillActivationResult
    {
        public bool Succeeded { get; internal set; }
        public SkillActivationFailureReason FailureReason { get; internal set; }
        public bool ConsumedSourceItem { get; internal set; }
        public int AppliedTargetCount { get; internal set; }
        public int RemainingUses { get; internal set; }
        public float RemainingCooldownSeconds { get; internal set; }

        public static SkillActivationResult Failure(SkillActivationFailureReason reason)
        {
            return new SkillActivationResult
            {
                FailureReason = reason
            };
        }
    }
}
