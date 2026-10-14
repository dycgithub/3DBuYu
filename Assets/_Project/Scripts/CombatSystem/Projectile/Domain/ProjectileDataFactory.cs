using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 在攻击数据和投射物数据之间规范化数值、生成偏移与范围球心。
    /// </summary>
    public static class ProjectileDataFactory
    {
        /// <summary>从最终攻击数据创建投射物数据。</summary>
        /// <param name="attack">攻击执行器生成的最终攻击数据。</param>
        /// <returns>可交给投射物模拟器的投射物数据。</returns>
        public static ProjectileData CreateFromAttack(in AttackData attack)
        {
            return new ProjectileData
            {
                AttackInstanceId = attack.AttackInstanceId,
                SourceInstanceId = attack.SourceInstanceId,
                TrackingTarget = attack.TrackingTarget,
                ProjectileProfile = attack.ProjectileProfile,
                OriginPosition = attack.OriginPosition,
                RangeOriginPosition = attack.OriginPosition,
                HasWorldRangeOrigin = true,
                ProjectileSpawnOffset = Mathf.Max(0f, attack.ProjectileSpawnOffset),
                LaunchDirection = attack.LaunchDirection,
                DamageAmount = attack.DamageAmount,
                EnergyCost = attack.EnergyCost,
                PaidEnergyCostSnapshot = attack.PaidEnergyCostSnapshot,
                HasPaidEnergyCostSnapshot = attack.HasPaidEnergyCostSnapshot,
                SpeedUnitsPerSecond = attack.SpeedUnitsPerSecond,
                MaximumTravelDistance = attack.MaximumTravelDistance,
                CollisionRadius = attack.CollisionRadius,
                PenetrationCount = attack.PenetrationCount,
                DamageType = attack.DamageType,
                WasCritical = attack.WasCritical,
                TargetMode = attack.TargetMode,
                AttacksPerSecond = attack.AttacksPerSecond
            };
        }

        /// <summary>使用显式参数创建规范化的投射物数据。</summary>
        /// <param name="attackInstanceId">所属攻击实例 ID。</param>
        /// <param name="sourceInstanceId">发起攻击的对象实例 ID。</param>
        /// <param name="projectileProfile">投射物配置。</param>
        /// <param name="originPosition">投射物发射起点。</param>
        /// <param name="launchDirection">投射物发射方向。</param>
        /// <param name="damageAmount">投射物伤害量。</param>
        /// <param name="wasCritical">是否为暴击攻击。</param>
        /// <returns>规范化后的投射物数据。</returns>
        public static ProjectileData CreateFromParameters(
            int attackInstanceId,
            int sourceInstanceId,
            ProjectileProfileSO projectileProfile,
            Vector3 originPosition,
            Vector3 launchDirection,
            float damageAmount,
            bool wasCritical = false,
            ProjectileTargetMode targetMode = ProjectileTargetMode.Enemy)
        {
            return new ProjectileData
            {
                AttackInstanceId = attackInstanceId,
                SourceInstanceId = sourceInstanceId,
                ProjectileProfile = projectileProfile,
                OriginPosition = originPosition,
                RangeOriginPosition = originPosition,
                HasWorldRangeOrigin = true,
                LaunchDirection = launchDirection,
                DamageAmount = Mathf.Max(0f, damageAmount),
                SpeedUnitsPerSecond = projectileProfile != null ? Mathf.Max(0f, projectileProfile.SpeedUnitsPerSecond) : 0f,
                MaximumTravelDistance = projectileProfile != null ? Mathf.Max(0f, projectileProfile.MaximumTravelDistance) : 0f,
                CollisionRadius = projectileProfile != null ? Mathf.Max(0f, projectileProfile.CollisionRadius) : 0f,
                DamageType = projectileProfile != null ? projectileProfile.DamageType : DamageType.Physical,
                WasCritical = wasCritical,
                TargetMode = targetMode
            };
        }
    }
}
