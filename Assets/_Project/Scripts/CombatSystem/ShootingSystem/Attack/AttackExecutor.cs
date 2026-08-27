using Services;

namespace CombatSystem
{
    /// <summary>
    /// 提交一次攻击的资源、子弹和冷却状态。
    /// </summary>
    public sealed class AttackExecutor
    {
        private readonly IProjectileSpawner _projectileSpawner;
        private readonly ICombatEnergyService _energy;
        private readonly ICombatPhaseService _phase;
        private readonly AttackCooldownRegistry _cooldowns;

        private int _nextAttackId;

        public AttackExecutor(
            IProjectileSpawner projectileSpawner,
            ICombatEnergyService energy,
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
            if (_projectileSpawner == null)
                return false;

            if (!_cooldowns.IsReady(attack.SourceId, attack.PortIndex, fireRate))
                return false;

            if (_energy != null)
            {
                if (!_energy.TrySpend(attack.EnergyCost, EnergySpendKind.Shot))
                    return false;

                // 精确扣到 0 时，耗尽事件会同步结束本局，不能继续生成子弹。
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

            _cooldowns.MarkUsed(attack.SourceId, attack.PortIndex);
            return true;
        }
    }
}
