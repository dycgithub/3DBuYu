using Interfaces;
using Services;
using SpatialIndexing;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>逐步转向最近有效目标的投射物轨迹。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Trajectory/Homing")]
    public sealed class HomingTrajectoryDefinitionSO : TrajectoryDefinitionSO
    {
        /// <summary>每秒允许的最大转向弧度。</summary>
        [Min(0f)] public float TurnRateRadiansPerSecond = 8f;
        /// <summary>当前目标死亡后重新搜索的最大半径。</summary>
        [Min(0f)] public float TargetSearchRadius = 50f;

        /// <summary>追踪轨迹由投射物扫掠处理，不执行 Hitscan。</summary>
        public override bool IsHitscan => false;

        /// <summary>首次生成时选取最近的有效目标。</summary>
        public override void InitializeRuntime(
            ProjectileRuntime projectile,
            ISpatialQueryService spatialQueryService)
        {
            if (projectile == null || projectile.TrackingTarget != null || spatialQueryService == null)
            {
                return;
            }

            projectile.TrackingTarget = FindTarget(projectile, spatialQueryService);
        }

        /// <summary>兼容未传入空间查询服务的旧调用。</summary>
        public override Vector3 GetDirection(ProjectileRuntime projectile, Vector3 currentDirection)
        {
            return TurnTowardTarget(projectile, currentDirection);
        }

        /// <summary>目标失效时重新搜寻，再以设定弧速度转向。</summary>
        public override Vector3 GetDirection(
            ProjectileRuntime projectile,
            Vector3 currentDirection,
            ISpatialQueryService spatialQueryService)
        {
            if (projectile != null &&
                (projectile.TrackingTarget == null || !projectile.TrackingTarget.IsAlive) &&
                spatialQueryService != null)
            {
                projectile.TrackingTarget = FindTarget(projectile, spatialQueryService);
            }

            return TurnTowardTarget(projectile, currentDirection);
        }

        /// <summary>按投射物目标阵营和搜寻范围查找最近目标。</summary>
        private IDamageable FindTarget(
            ProjectileRuntime projectile,
            ISpatialQueryService spatialQueryService)
        {
            int layerMask = projectile.Data.TargetMode == ProjectileTargetMode.Player
                ? SpatialLayers.Player
                : SpatialLayers.Enemy;
            return spatialQueryService.QueryNearest(
                projectile.Position,
                Mathf.Max(0f, TargetSearchRadius),
                layerMask);
        }

        /// <summary>沿当前有效目标方向以限定弧速度转向。</summary>
        private Vector3 TurnTowardTarget(ProjectileRuntime projectile, Vector3 currentDirection)
        {
            if (projectile == null || projectile.TrackingTarget == null ||
                !projectile.TrackingTarget.IsAlive)
            {
                return currentDirection;
            }

            Vector3 targetDirection = projectile.TrackingTarget.Position - projectile.Position;
            if (targetDirection.sqrMagnitude <= 0.0001f)
            {
                return currentDirection;
            }

            Vector3 direction = currentDirection.sqrMagnitude > 0.0001f
                ? currentDirection.normalized
                : Vector3.forward;
            float maximumRadians = Mathf.Max(0f, TurnRateRadiansPerSecond) *
                Mathf.Max(0f, projectile.SimulationDeltaTime);
            return Vector3.RotateTowards(
                direction,
                targetDirection.normalized,
                maximumRadians,
                0f).normalized;
        }
    }
}
