using Services;

/// <summary>
/// 为 CombatLab 提供始终允许战斗行为的阶段服务。
/// </summary>
public sealed class CombatLabPhaseService : ICombatPhaseService
{
    /// <summary>CombatLab 默认允许创建攻击和技能行为。</summary>
    public bool CanPerformCombatActions => true;
}
