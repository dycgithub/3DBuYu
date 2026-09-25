namespace GameSystem
{
    /// <summary>
    /// 表示本局规则判定出的最终结果。
    /// </summary>
    public enum RunVerdict
    {
        /// <summary>满足本局完成条件。</summary>
        Victory,

        /// <summary>未满足本局完成条件。</summary>
        Defeat
    }

    /// <summary>
    /// 本局唯一的自动终局裁判。
    /// 时间、敌群和能量服务只发布事实，由调用方在能量耗尽事实发生时请求判定。
    /// </summary>
    public sealed class RunRuleService
    {
        /// <summary>判断目标时长到达时是否应立即失败。</summary>
        /// <param name="elapsedTime">本局已运行时间。</param>
        /// <param name="targetDuration">目标时长。</param>
        /// <param name="killCount">本局击杀数。</param>
        /// <param name="targetKillCount">最低击杀数。</param>
        /// <returns>达到时长但击杀不足时返回 true。</returns>
        public bool ShouldFailAtTarget(
            float elapsedTime,
            float targetDuration,
            int killCount,
            int targetKillCount)
        {
            return RunRuleMath.HasReachedTargetDuration(elapsedTime, targetDuration) &&
                   !RunRuleMath.HasRequiredKills(killCount, targetKillCount);
        }

        /// <summary>判断本局是否满足进入超时阶段的条件。</summary>
        /// <param name="elapsedTime">本局已运行时间。</param>
        /// <param name="targetDuration">目标时长。</param>
        /// <param name="killCount">本局击杀数。</param>
        /// <param name="targetKillCount">最低击杀数。</param>
        /// <returns>达到时长且击杀达标时返回 true。</returns>
        public bool ShouldEnterOvertime(
            float elapsedTime,
            float targetDuration,
            int killCount,
            int targetKillCount)
        {
            return RunRuleMath.HasReachedTargetDuration(elapsedTime, targetDuration) &&
                   RunRuleMath.HasRequiredKills(killCount, targetKillCount);
        }

        /// <summary>
        /// 根据能量耗尽时的本局进度判定胜负。
        /// </summary>
        /// <param name="elapsedTime">本局已运行时间。</param>
        /// <param name="targetDuration">要求达到的目标时长。</param>
        /// <param name="killCount">本局已完成的击杀数。</param>
        /// <param name="targetKillCount">要求达到的击杀数。</param>
        /// <returns>满足规则时返回胜利，否则返回失败。</returns>
        public RunVerdict JudgeEnergyDepleted(
            float elapsedTime,
            float targetDuration,
            int killCount,
            int targetKillCount)
        {
            return RunRuleMath.MeetsRunRequirements(
                elapsedTime,
                targetDuration,
                killCount,
                targetKillCount)
                ? RunVerdict.Victory
                : RunVerdict.Defeat;
        }
    }
}
