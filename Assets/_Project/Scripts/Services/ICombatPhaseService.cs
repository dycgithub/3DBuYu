namespace Services
{
    /// <summary>
    /// 告诉攻击和技能系统当前是否允许创建新的战斗行为。
    /// 这样攻击模块不需要直接依赖 GameManager。
    /// </summary>
    public interface ICombatPhaseService
    {
        bool CanPerformCombatActions { get; }
    }
}
