using Services;

namespace _Project.Tests.CombatPlayground.Implement
{
    /// <summary>
    /// 让独立实验场景始终允许手动战斗行为。
    /// </summary>
    public sealed class CombatPlaygroundPhaseService : ICombatPhaseService
    {
        /// <summary>实验场景不经过正式战斗阶段切换。</summary>
        public bool CanPerformCombatActions => true;
    }
}
