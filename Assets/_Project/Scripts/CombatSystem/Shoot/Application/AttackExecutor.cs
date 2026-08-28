using Services;

namespace CombatSystem
{
    public sealed class AttackExecutor
    {
        private readonly IProjectileSpawner _projectileSpawner;
        private readonly IEnergyService _energy;
        private readonly ICombatPhaseService _phase;
        private readonly AttackCooldownRegistry _cooldowns;
        private int _nextAttackId;

        public AttackExecutor(
            IProjectileSpawner projectileSpawner,
            IEnergyService energy,
            ICombatPhaseService phase,
            AttackCooldownRegistry cooldowns)
        {
            _projectileSpawner = projectileSpawner;
            _energy = energy;
            _phase = phase;
            _cooldowns = cooldowns;
        }

        public bool TryExecute(ref AttackInfo attack, float fireRate)
        {
            if (_projectileSpawner == null || _cooldowns == null)
                return false;
            if (_phase != null && !_phase.CanPerformCombatActions)
                return false;
            if (!_cooldowns.IsReady(attack.SourceId, attack.TransmitterIndex, fireRate))
                return false;

            if (_energy != null)
            {
                if (!_energy.TrySpend(attack.EnergyCost, EnergySpendKind.Shot))
                    return false;
                if (_phase != null && !_phase.CanPerformCombatActions)
                    return false;
            }

            attack.AttackId = ++_nextAttackId;
            ProjectileInfo projectile = ProjectileInfoFactory.FromAttack(in attack);
            if (!_projectileSpawner.TrySpawnBatch(in projectile, attack.ProjectileCount))
            {
                if (_energy != null && attack.EnergyCost > 0f)
                    _energy.AddEnergy(attack.EnergyCost * _energy.CostMultiplier);
                return false;
            }

            _cooldowns.MarkUsed(attack.SourceId, attack.TransmitterIndex);
            return true;
        }
    }
}
