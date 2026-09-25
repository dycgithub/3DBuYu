namespace CombatSystem
{
    /// <summary>
    /// 直接执行技能动作的边界，不负责玩家规则或资源消费。
    /// </summary>
    public interface ISkillExecutionService
    {
        /// <summary>
        /// 执行一次已经准备好的技能请求。
        /// </summary>
        /// <param name="request">包含技能、来源和目标快照的请求。</param>
        /// <returns>技能动作执行结果。</returns>
        SkillExecutionResult Execute(in SkillExecutionRequest request);
    }
}
