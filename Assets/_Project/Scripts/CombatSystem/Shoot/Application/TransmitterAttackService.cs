using Services;

namespace CombatSystem
{
    public sealed class TransmitterAttackService : ITransmitterAttackService
    {
        private readonly ICombatPhaseService _phase;
        private readonly TransmitterShootBuildService _buildService;
        private readonly AttackBuilder _attackBuilder;
        private readonly AttackExecutor _attackExecutor;

        public TransmitterAttackService(
            ICombatPhaseService phase,
            TransmitterShootBuildService buildService,
            AttackBuilder attackBuilder,
            AttackExecutor attackExecutor)
        {
            _phase = phase;
            _buildService = buildService;
            _attackBuilder = attackBuilder;
            _attackExecutor = attackExecutor;
        }

        public bool TryExecute(in TransmitterAttackInput input)
        {
            if (_phase != null && !_phase.CanPerformCombatActions)
                return false;
            if (_buildService == null || _attackBuilder == null || _attackExecutor == null)
                return false;

            TransmitterShootBuild build = _buildService.Build(in input);
            if (!_attackBuilder.TryBuild(in input, in build, out AttackInfo attack))
                return false;

            return _attackExecutor.TryExecute(ref attack, input.FireRate);
        }
    }
}
