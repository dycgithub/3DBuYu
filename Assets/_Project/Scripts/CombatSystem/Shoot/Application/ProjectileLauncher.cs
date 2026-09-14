using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 将通用发射请求转换为投射物数据并提交给模拟器。
    /// <para>本服务不负责资源、冷却、战斗阶段或瞄准；玩家和敌人都可以直接调用。</para>
    /// </summary>
    public sealed class ProjectileLauncher : IProjectileLauncher
    {
        private readonly IProjectileSpawner _projectileSpawner;
        private readonly IAttackInstanceIdGenerator _attackInstanceIdGenerator;

        /// <summary>创建直接投射物发射器。</summary>
        /// <param name="projectileSpawner">负责投射物生命周期的生成边界。</param>
        /// <param name="attackInstanceIdGenerator">统一分配攻击实例标识的服务；为空时创建本地生成器。</param>
        public ProjectileLauncher(
            IProjectileSpawner projectileSpawner,
            IAttackInstanceIdGenerator attackInstanceIdGenerator = null)
        {
            _projectileSpawner = projectileSpawner;
            _attackInstanceIdGenerator = attackInstanceIdGenerator ?? new AttackInstanceIdGenerator();
        }

        /// <summary>
        /// 直接发射投射物，不检查任何资源或战斗规则。
        /// </summary>
        /// <param name="request">已经准备好的发射请求。</param>
        /// <returns>请求被投射物模拟器接受时返回 <c>true</c>。</returns>
        public bool Launch(in ProjectileLaunchRequest request)
        {
            if (_projectileSpawner == null
                || request.ProjectileProfile == null
                || request.ProjectileCount <= 0
                || request.DamageAmount < 0f)
            {
                return false;
            }

            int penetrationCount = Mathf.Max(0, request.PenetrationCount);
            if (request.ProjectileProfile.TrajectoryDefinition != null
                && request.ProjectileProfile.TrajectoryDefinition.IsHitscan
                && penetrationCount > 0)
            {
                return false;
            }

            AttackData attackData = new()
            {
                AttackInstanceId = _attackInstanceIdGenerator.Next(),
                SourceInstanceId = request.SourceInstanceId,
                TransmitterPortIndex = -1,
                ProjectileProfile = request.ProjectileProfile,
                OriginPosition = request.OriginPosition,
                LaunchDirection = request.LaunchDirection,
                DamageAmount = Mathf.Max(0f, request.DamageAmount),
                // 直接入口不支付 ProjectileProfile 中配置的资源消耗。
                EnergyCost = 0f,
                ProjectileCount = request.ProjectileCount,
                PenetrationCount = penetrationCount,
                DamageType = request.ProjectileProfile.DamageType,
                SpeedUnitsPerSecond = Mathf.Max(
                    0f,
                    request.ProjectileProfile.SpeedUnitsPerSecond),
                MaximumTravelDistance = Mathf.Max(
                    0f,
                    request.ProjectileProfile.MaximumTravelDistance),
                CollisionRadius = Mathf.Max(
                    0f,
                    request.ProjectileProfile.CollisionRadius),
                WasCritical = request.WasCritical,
                TargetMode = request.TargetMode
            };

            return Launch(in attackData);
        }

        /// <summary>
        /// 提交已经构建完成的攻击数据，不检查资源、冷却或战斗阶段。
        /// </summary>
        /// <param name="attackData">已经包含最终攻击参数的数据。</param>
        /// <returns>投射物生成请求被接受时返回 <c>true</c>。</returns>
        public bool Launch(in AttackData attackData)
        {
            if (_projectileSpawner == null
                || attackData.ProjectileProfile == null
                || attackData.ProjectileCount <= 0
                || attackData.DamageAmount < 0f)
            {
                return false;
            }

            int penetrationCount = Mathf.Max(0, attackData.PenetrationCount);
            if (attackData.ProjectileProfile.TrajectoryDefinition != null
                && attackData.ProjectileProfile.TrajectoryDefinition.IsHitscan
                && penetrationCount > 0)
            {
                return false;
            }

            AttackData normalizedAttack = attackData;
            normalizedAttack.PenetrationCount = penetrationCount;
            normalizedAttack.ProjectileCount = Mathf.Max(1, attackData.ProjectileCount);
            if (normalizedAttack.AttackInstanceId <= 0)
            {
                normalizedAttack.AttackInstanceId = _attackInstanceIdGenerator.Next();
            }
            normalizedAttack.DamageAmount = Mathf.Max(0f, attackData.DamageAmount);
            ProjectileData projectileData = ProjectileDataFactory.CreateFromAttack(
                in normalizedAttack);
            return _projectileSpawner.TrySpawnProjectileBatch(
                in projectileData,
                normalizedAttack.ProjectileCount);
        }
    }
}
