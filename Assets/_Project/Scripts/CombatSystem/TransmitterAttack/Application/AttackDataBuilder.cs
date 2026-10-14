using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 将发射器输入（包括显式追踪目标）和已汇总修正值转换为最终攻击数据。
    /// </summary>
    public sealed class AttackDataBuilder
    {
        /// <summary>
        /// 尝试构建一份可提交给攻击执行器的攻击数据。
        /// </summary>
        /// <param name="input">发射器端口的原始输入。</param>
        /// <param name="build">装备修正汇总结果。</param>
        /// <param name="attackData">构建成功时输出的攻击数据。</param>
        /// <returns>投射物配置有效时返回 true。</returns>
        public bool TryBuildAttackData(
            in TransmitterAttackInput input,
            in TransmitterAttackBuild build,
            out AttackData attackData)
        {
            attackData = default;
            if (build.ProjectileProfile == null)
            {
                return false;
            }

            if (build.ProjectileProfile.TrajectoryDefinition != null
                && build.ProjectileProfile.TrajectoryDefinition.IsHitscan
                && build.PenetrationCount > 0)
            {
                return false;
            }

            Vector3 launchDirection = input.LaunchDirection.sqrMagnitude > 0.0001f
                ? input.LaunchDirection.normalized
                : Vector3.forward;

            float damageAmount = Mathf.Max(
                0f,
                input.BaseDamage + build.AdditiveDamageBonus);
            damageAmount *= build.DamageMultiplier;

            bool wasCritical = Random.value < build.CriticalChance;
            if (wasCritical)
            {
                damageAmount *= build.CriticalDamageMultiplier;
            }

            float attacksPerSecond = Mathf.Max(0.001f, build.AttacksPerSecond);
            if (build.ProjectileProfile.TrajectoryDefinition is LaserTrajectoryDefinitionSO laser)
            {
                float tickRate = Mathf.Max(1f, laser.TicksPerSecond);
                damageAmount *= attacksPerSecond / tickRate;
                attacksPerSecond = tickRate;
            }

            attackData = new AttackData
            {
                SourceInstanceId = input.SourceInstanceId,
                TransmitterPortIndex = input.TransmitterPortIndex,
                TrackingTarget = input.TrackingTarget,
                ProjectileProfile = build.ProjectileProfile,
                OriginPosition = input.OriginPosition,
                ProjectileSpawnOffset = Mathf.Max(0f, input.ProjectileSpawnOffset),
                LaunchDirection = launchDirection,
                DamageAmount = Mathf.Max(0f, damageAmount),
                EnergyCost = Mathf.Max(0f,
                    build.ProjectileProfile.EnergyCost * build.EnergyCostMultiplier),
                AttacksPerSecond = attacksPerSecond,
                ProjectileCount = Mathf.Max(1, build.ProjectileCount),
                PenetrationCount = Mathf.Max(0, build.PenetrationCount),
                DamageType = build.DamageType,
                SpeedUnitsPerSecond = Mathf.Max(0f, build.ProjectileProfile.SpeedUnitsPerSecond),
                MaximumTravelDistance = Mathf.Max(
                    0f,
                    Mathf.Min(
                        Mathf.Max(0f, input.BaseRange) * build.RangeMultiplier,
                        Mathf.Max(0f, build.ProjectileProfile.MaximumTravelDistance))),
                CollisionRadius = Mathf.Max(0f, build.CollisionRadius),
                WasCritical = wasCritical
            };
            return true;
        }
    }
}
