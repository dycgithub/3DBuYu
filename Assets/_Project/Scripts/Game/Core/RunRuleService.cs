namespace GameSystem
{
    public enum RunVerdict
    {
        Victory,
        Defeat
    }

    /// <summary>
    /// 本局唯一的自动终局裁判。
    /// 时间、波次和能量服务只发布事实，由调用方在能量耗尽事实发生时请求判定。
    /// </summary>
    public sealed class RunRuleService
    {
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
