namespace CombatSystem
{
    /// <summary>
    /// 无前置技能执行失败原因。
    /// 资源、战斗阶段和冷却属于外层规则，不在此枚举中处理。
    /// </summary>
    public enum SkillExecutionFailureReason
    {
        None,
        SkillNotFound,
        NoValidTarget,
        ActionFailed
    }
}
