using Services;

namespace CombatSystem
{
    /// <summary>
    /// 校验攻击阶段、冷却和能量，并把攻击提交给投射物生成器。
    /// </summary>
    public sealed class AttackExecutor
    {
        private readonly IProjectileSpawner _projectileSpawner;
        private readonly IProjectileLauncher _projectileLauncher;
        private readonly IEnergyService _energy;
        private readonly ICombatPhaseService _phase;
        private readonly AttackCooldownRegistry _cooldowns;
        private readonly IAttackInstanceIdGenerator _attackInstanceIdGenerator;

        /// <summary>创建攻击执行器。</summary>
        /// <param name="projectileSpawner">负责提交投射物生成的服务。</param>
        /// <param name="energy">提供能量扣除和回滚的服务。</param>
        /// <param name="phase">提供战斗阶段门禁的服务。</param>
        /// <param name="cooldowns">维护端口攻击冷却的注册表。</param>
        /// <param name="projectileLauncher">可选的通用投射物提交器；为空时使用旧生成边界。</param>
        /// <param name="attackInstanceIdGenerator">统一分配攻击实例标识的服务；为空时创建本地生成器。</param>
        public AttackExecutor(
            IProjectileSpawner projectileSpawner,
            IEnergyService energy,
            ICombatPhaseService phase,
            AttackCooldownRegistry cooldowns,
            IProjectileLauncher projectileLauncher = null,
            IAttackInstanceIdGenerator attackInstanceIdGenerator = null)
        {
            _projectileSpawner = projectileSpawner;
            _projectileLauncher = projectileLauncher;
            _energy = energy;
            _phase = phase;
            _cooldowns = cooldowns;
            _attackInstanceIdGenerator = attackInstanceIdGenerator ?? new AttackInstanceIdGenerator();
        }

        /// <summary>
        /// 尝试执行一份攻击数据；失败时不生成投射物并回滚已扣除能量。
        /// </summary>
        /// <param name="attackData">待执行的攻击数据。</param>
        /// <param name="attacksPerSecond">当前端口每秒允许的攻击次数。</param>
        /// <returns>攻击成功提交时返回 true。</returns>
        public bool TryExecuteAttack(ref AttackData attackData, float attacksPerSecond)
        {
            // 即时命中和资源提交结束后，才允许能量耗尽事件清理战局。
            using System.IDisposable action = (_energy as IEnergyActionScope)?.BeginAction();
            if ((_projectileSpawner == null && _projectileLauncher == null)
                || _cooldowns == null)
            {
                return false;
            }

            if (_phase != null && !_phase.CanPerformCombatActions)
            {
                return false;
            }

            float effectiveAttacksPerSecond = attackData.AttacksPerSecond > 0f
                ? attackData.AttacksPerSecond
                : attacksPerSecond;
            if (!_cooldowns.IsAttackReady(
                    attackData.SourceInstanceId,
                    attackData.TransmitterPortIndex,
                    effectiveAttacksPerSecond))
            {
                return false;
            }

            if (_energy != null)
            {
                if (!_energy.TrySpend(attackData.EnergyCost, EnergySpendKind.Shot))
                {
                    return false;
                }

                if (_phase != null && !_phase.CanPerformCombatActions)
                {
                    RefundEnergy(attackData.EnergyCost);
                    return false;
                }
            }

            attackData.AttackInstanceId = _attackInstanceIdGenerator.Next();
            bool spawned = _projectileLauncher != null
                ? _projectileLauncher.Launch(in attackData)
                : TrySpawnWithLegacySpawner(in attackData);
            if (!spawned && _projectileLauncher != null && _projectileSpawner != null)
            {
                spawned = TrySpawnWithLegacySpawner(in attackData);
            }

            if (!spawned)
            {
                RefundEnergy(attackData.EnergyCost);
                return false;
            }

            _cooldowns.RecordAttack(
                attackData.SourceInstanceId,
                attackData.TransmitterPortIndex);
            return true;
        }

        /// <summary>使用旧投射物边界提交攻击数据。</summary>
        /// <param name="attackData">已经设置攻击实例标识的数据。</param>
        /// <returns>生成器接受请求时返回 <c>true</c>。</returns>
        private bool TrySpawnWithLegacySpawner(in AttackData attackData)
        {
            ProjectileData projectileData = ProjectileDataFactory.CreateFromAttack(
                in attackData);
            return _projectileSpawner.TrySpawnProjectileBatch(
                in projectileData,
                attackData.ProjectileCount);
        }

        /// <summary>退还本次攻击已经扣除的能量。</summary>
        /// <param name="baseEnergyCost">攻击数据中的基础能量消耗。</param>
        private void RefundEnergy(float baseEnergyCost)
        {
            if (_energy != null && baseEnergyCost > 0f)
            {
                _energy.AddEnergy(baseEnergyCost);
            }
        }
    }
}
