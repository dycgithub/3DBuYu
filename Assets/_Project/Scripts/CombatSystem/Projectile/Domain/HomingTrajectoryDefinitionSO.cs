using System.Collections.Generic;
using Interfaces;
using Services;
using SpatialIndexing;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>逐步转向最近有效且尚未命中过的目标。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Trajectory/Homing")]
    public sealed class HomingTrajectoryDefinitionSO : TrajectoryDefinitionSO
    {
        /// <summary>每秒允许的最大转向弧度。</summary>
        [Min(0f)] public float TurnRateRadiansPerSecond = 8f;
        /// <summary>当前目标死亡后重新搜索的最大半径。</summary>
        [Min(0f)] public float TargetSearchRadius = 50f;
        /// <summary>全场模式不受半径限制，每颗子弹在自身位置独立选择最近目标。</summary>
        public HomingTargetSearch SearchMode;

        /// <summary>追踪轨迹由投射物扫掠处理，不执行 Hitscan。</summary>
        public override bool IsHitscan => false;

        /// <summary>首次生成时选取最近的有效目标。</summary>
        public override void InitializeRuntime(
            ProjectileState projectile,
            ISpatialQueryService spatialQueryService)
        {
            if (projectile == null
                || IsValidTarget(projectile.TrackingTarget)
                || spatialQueryService == null)
            {
                return;
            }

            projectile.TrackingTarget = FindTarget(projectile, spatialQueryService);
        }

        /// <summary>兼容未传入空间查询服务的旧调用。</summary>
        public override Vector3 GetDirection(ProjectileState projectile, Vector3 currentDirection)
        {
            return TurnTowardTarget(projectile, currentDirection);
        }

        /// <summary>目标失效时重新搜寻，再以设定弧速度转向。</summary>
        public override Vector3 GetDirection(
            ProjectileState projectile,
            Vector3 currentDirection,
            ISpatialQueryService spatialQueryService)
        {
            if (projectile != null
                && !IsValidTarget(projectile.TrackingTarget)
                && spatialQueryService != null)
            {
                projectile.TrackingTarget = FindTarget(projectile, spatialQueryService);
            }

            return TurnTowardTarget(projectile, currentDirection);
        }

        /// <summary>按阵营、搜索模式和已命中记录查找最近有效目标。</summary>
        private IDamageable FindTarget(
            ProjectileState projectile,
            ISpatialQueryService spatialQueryService)
        {
            float searchRadius = Mathf.Max(0f, TargetSearchRadius);
            if (SearchMode == HomingTargetSearch.WithinRadius && searchRadius <= 0f)
            {
                return null;
            }

            int layerMask = projectile.Data.TargetMode == ProjectileTargetMode.Player
                ? SpatialLayers.Player
                : SpatialLayers.Enemy;
            if (SearchMode == HomingTargetSearch.WithinRadius && projectile.HitTargetInstanceIds.Count == 0)
            {
                return spatialQueryService.QueryNearest(
                    projectile.Position,
                    searchRadius,
                    layerMask);
            }

            List<IDamageable> targets = SearchMode == HomingTargetSearch.AllLivingTargets
                ? spatialQueryService.QueryAll(layerMask)
                : spatialQueryService.QueryRadius(projectile.Position, searchRadius, layerMask);
            if (targets == null)
            {
                return null;
            }

            float nearestDistanceSquared = SearchMode == HomingTargetSearch.AllLivingTargets
                ? float.PositiveInfinity : searchRadius * searchRadius;
            IDamageable nearestTarget = null;

            for (int index = 0; index < targets.Count; index++)
            {
                IDamageable candidate = targets[index];
                if (!IsValidTarget(candidate) || candidate.Transform == null)
                {
                    continue;
                }

                int targetInstanceId = candidate.Transform.GetInstanceID();
                if (targetInstanceId == 0 || projectile.HitTargetInstanceIds.Contains(targetInstanceId))
                {
                    continue;
                }

                float distanceSquared = (candidate.Position - projectile.Position).sqrMagnitude;
                if (distanceSquared >= nearestDistanceSquared)
                {
                    continue;
                }

                nearestDistanceSquared = distanceSquared;
                nearestTarget = candidate;
            }

            return nearestTarget;
        }

        /// <summary>沿当前有效目标方向以限定弧速度转向。</summary>
        private Vector3 TurnTowardTarget(ProjectileState projectile, Vector3 currentDirection)
        {
            if (projectile == null || !IsValidTarget(projectile.TrackingTarget))
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

        /// <summary>全场搜索还排除失活对象，避免池中仍标记存活的敌人被选中。</summary>
        private bool IsValidTarget(IDamageable target)
        {
            return target.IsAliveAndValid() &&
                (SearchMode != HomingTargetSearch.AllLivingTargets ||
                 target.Transform != null && target.Transform.gameObject.activeInHierarchy);
        }
    }
}
