using Interfaces;
using Services;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>以短生命周期和固定扫掠半径连续命中多名敌人的轨迹。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Trajectory/Laser")]
    public sealed class LaserTrajectoryDefinitionSO : TrajectoryDefinitionSO
    {
        /// <summary>激光扫掠半径。</summary>
        [Min(0f)] public float Radius = 0.35f;
        /// <summary>激光持续时间。</summary>
        [Min(0.01f)] public float LifetimeSeconds = 0.15f;
        /// <summary>用于维持常规每秒伤害的激光脉冲频率。</summary>
        [Min(1f)] public float TicksPerSecond = 10f;

        /// <summary>激光使用连续扫掠，不执行 Hitscan。</summary>
        public override bool IsHitscan => false;

        /// <summary>将配置半径并入投射物碰撞范围。</summary>
        public override void ConfigureData(ref ProjectileData projectileData)
        {
            projectileData.CollisionRadius = Mathf.Max(
                projectileData.CollisionRadius,
                Mathf.Max(0f, Radius));
            projectileData.SpeedUnitsPerSecond = Mathf.Max(
                projectileData.SpeedUnitsPerSecond,
                projectileData.MaximumTravelDistance * Mathf.Max(1f, TicksPerSecond));
        }

        /// <summary>将生命时间收紧到激光配置值。</summary>
        public override void InitializeRuntime(
            ProjectileRuntime projectile,
            ISpatialQueryService spatialQueryService)
        {
            if (projectile != null)
            {
                projectile.RemainingLifetimeSeconds = Mathf.Min(
                    projectile.RemainingLifetimeSeconds,
                    Mathf.Max(0.01f, LifetimeSeconds));
            }
        }

        /// <summary>一次扫掠命中后继续检测沿途后续目标。</summary>
        public override bool ContinuesAfterHit => true;
    }
}
