namespace CombatSystem
{
    /// <summary>
    /// 无前置技能执行结果。
    /// </summary>
    public readonly struct SkillExecutionResult
    {
        /// <summary>是否完成技能动作。</summary>
        public bool Succeeded { get; }

        /// <summary>技能执行失败原因。</summary>
        public SkillExecutionFailureReason FailureReason { get; }

        /// <summary>实际提交并生效的目标数量。</summary>
        public int AppliedTargetCount { get; }

        /// <summary>创建技能执行结果。</summary>
        /// <param name="succeeded">是否执行成功。</param>
        /// <param name="failureReason">失败原因。</param>
        /// <param name="appliedTargetCount">实际生效目标数量。</param>
        public SkillExecutionResult(
            bool succeeded,
            SkillExecutionFailureReason failureReason,
            int appliedTargetCount)
        {
            Succeeded = succeeded;
            FailureReason = failureReason;
            AppliedTargetCount = appliedTargetCount;
        }

        /// <summary>创建失败结果。</summary>
        /// <param name="reason">失败原因。</param>
        /// <returns>失败结果。</returns>
        public static SkillExecutionResult Failure(SkillExecutionFailureReason reason)
        {
            return new SkillExecutionResult(false, reason, 0);
        }
    }
}
