using Services;

namespace CombatSystem
{
    /// <summary>
    /// 执行炮台适配层提交的攻击命令。
    /// 场景对象、输入和目标选择均由 Play 层在命令生成阶段处理。
    /// </summary>
    public sealed class TransmitterAttackCoordinator
    {
        private readonly ICombatPhaseService _phase;
        private readonly AttackBuilder _attackBuilder;
        private readonly AttackExecutor _attackExecutor;

        public TransmitterAttackCoordinator(
            ICombatPhaseService phase,
            AttackBuilder attackBuilder,
            AttackExecutor attackExecutor)
        {
            _phase = phase;
            _attackBuilder = attackBuilder;
            _attackExecutor = attackExecutor;
        }

        public bool TryExecute(in PortAttackContext context)
        {
            if (_attackBuilder == null || _attackExecutor == null)
                return false;

            if (_phase != null && !_phase.CanPerformCombatActions)
                return false;

            if (!_attackBuilder.TryBuild(in context, out AttackInfo attack))
                return false;

            return _attackExecutor.TryExecute(ref attack, context.FireRate);
        }
    }
}
