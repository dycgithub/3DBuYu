using System.Collections.Generic;
using Interfaces;
using Services;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 推进单个投射物，并在射程球内处理轨迹、碰撞、命中和过期状态。
    /// </summary>
    internal sealed class ProjectileStepper
    {
        private readonly IProjectileCollisionQuery _collisionQuery;
        private readonly ProjectileHitResolver _hitResolver;
        private readonly ISpatialQueryService _spatialQueryService;
        private readonly List<ProjectileCollision> _collisionResults = new();

        /// <summary>连接投射物碰撞、命中和空间查询能力。</summary>
        /// <param name="collisionQuery">查询投射物路径碰撞的服务。</param>
        /// <param name="hitResolver">提交命中并处理跳弹和分裂的服务。</param>
        /// <param name="spatialQueryService">提供目标查询的服务。</param>
        internal ProjectileStepper(
            IProjectileCollisionQuery collisionQuery,
            ProjectileHitResolver hitResolver,
            ISpatialQueryService spatialQueryService)
        {
            _collisionQuery = collisionQuery;
            _hitResolver = hitResolver;
            _spatialQueryService = spatialQueryService;
        }

        /// <summary>推进投射物一个时间步，处理路径碰撞和过期状态。</summary>
        /// <param name="projectile">待推进的投射物状态。</param>
        /// <param name="deltaTime">本次模拟经过的秒数。</param>
        internal void Simulate(ProjectileState projectile, float deltaTime)
        {
            if (projectile.IsCompleted)
            {
                return;
            }

            ProjectileProfileSO profile = projectile.Data.ProjectileProfileSnapshot;
            TrajectoryDefinitionSO trajectory = profile != null
                ? profile.TrajectoryDefinition
                : null;
            projectile.SimulationDeltaTime = deltaTime;
            projectile.DirectionChangedThisStep = false;
            Vector3 direction = trajectory != null
                ? trajectory.GetDirection(
                    projectile,
                    projectile.Direction,
                    _spatialQueryService).normalized
                : projectile.Direction;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.forward;
            }

            projectile.Direction = direction;
            projectile.Position = ProjectileLaunchPattern.ClampToWorldRange(
                projectile.Position,
                projectile.RangeOriginPosition,
                projectile.Data.MaximumTravelDistance);
            float remainingDistance = Mathf.Max(
                0f,
                projectile.Data.MaximumTravelDistance - projectile.TraveledDistance);
            bool canContinueBeyondTravelLimit = trajectory != null &&
                trajectory.CanContinueBeyondTravelLimit(projectile);
            if (remainingDistance <= 0f && !canContinueBeyondTravelLimit)
            {
                CompleteExpired(projectile);
                return;
            }

            float travelDistance = projectile.Data.SpeedUnitsPerSecond * deltaTime +
                projectile.PendingTravelDistance;
            projectile.PendingTravelDistance = 0f;
            if (!canContinueBeyondTravelLimit)
            {
                travelDistance = Mathf.Min(travelDistance, remainingDistance);
            }

            if (trajectory is BoomerangTrajectoryDefinitionSO && projectile.IsReturning)
            {
                travelDistance = Mathf.Min(
                    travelDistance,
                    Vector3.Distance(projectile.Position, projectile.ReturnPosition));
            }

            travelDistance = Mathf.Min(
                travelDistance,
                ProjectileLaunchPattern.GetWorldRangeStepDistance(
                    projectile.Position,
                    direction,
                    projectile.RangeOriginPosition,
                    projectile.Data.MaximumTravelDistance));

            if (travelDistance <= 0f)
            {
                bool rangeExpired = UpdateWorldRangeBoundary(projectile, trajectory);
                projectile.RemainingLifetimeSeconds -= deltaTime;
                UpdateView(projectile);
                if (!projectile.IsCompleted && trajectory != null &&
                    trajectory.ShouldComplete(projectile))
                {
                    CompleteExpired(projectile);
                }

                if (!projectile.IsCompleted && projectile.RemainingLifetimeSeconds <= 0f)
                {
                    CompleteExpired(projectile);
                }

                if (!projectile.IsCompleted && rangeExpired)
                {
                    CompleteExpired(projectile);
                }

                return;
            }

            bool hasCollision = false;
            bool rangeQueryUsed = false;
            float firstCollisionDistance = 0f;
            if (trajectory != null && trajectory.ContinuesAfterHit &&
                _collisionQuery is IProjectileCollisionRangeQuery rangeQuery)
            {
                rangeQueryUsed = true;
                _collisionResults.Clear();
                int collisionCount = rangeQuery.QuerySweepCollisions(
                    projectile,
                    travelDistance,
                    _collisionResults);
                if (collisionCount > 0)
                {
                    ProjectileCollision firstCollision = _collisionResults[0];
                    projectile.Position = firstCollision.Hit.point;
                    projectile.TraveledDistance += firstCollision.Hit.distance;
                    firstCollisionDistance = firstCollision.Hit.distance;
                    for (int index = 0; index < _collisionResults.Count; index++)
                    {
                        ProjectileCollision collision = _collisionResults[index];
                        _hitResolver.ApplyProjectileHit(
                            projectile,
                            collision.Target,
                            collision.Hit.point,
                            collision.Hit.normal);
                        if (projectile.DirectionChangedThisStep || projectile.IsCompleted)
                        {
                            break;
                        }
                    }

                    hasCollision = true;
                }
            }

            if (!rangeQueryUsed && !hasCollision && _collisionQuery != null &&
                _collisionQuery.TryFindSweepCollision(
                    projectile,
                    travelDistance,
                    out RaycastHit hit,
                    out IDamageable target))
            {
                projectile.Position = hit.point;
                projectile.TraveledDistance += hit.distance;
                firstCollisionDistance = hit.distance;
                _hitResolver.ApplyProjectileHit(projectile, target, hit.point, hit.normal);
                hasCollision = true;
            }

            if (!hasCollision)
            {
                projectile.Position += direction * travelDistance;
                projectile.TraveledDistance += travelDistance;
            }

            if (hasCollision && !projectile.IsCompleted)
            {
                if (projectile.DirectionChangedThisStep)
                {
                    projectile.PendingTravelDistance = Mathf.Max(
                        0f,
                        travelDistance - firstCollisionDistance);
                    projectile.Position += projectile.Direction * 0.01f;
                }
                else if (trajectory != null && trajectory.ContinuesAfterHit)
                {
                    float remainingAfterHit = Mathf.Max(0f, travelDistance - firstCollisionDistance);
                    projectile.Position += direction * remainingAfterHit;
                    projectile.TraveledDistance += remainingAfterHit;
                }
                else
                {
                    projectile.Position += projectile.Direction * 0.01f;
                }
            }

            projectile.Position = ProjectileLaunchPattern.ClampToWorldRange(
                projectile.Position,
                projectile.RangeOriginPosition,
                projectile.Data.MaximumTravelDistance);
            bool worldRangeExpired = UpdateWorldRangeBoundary(projectile, trajectory);
            projectile.RemainingLifetimeSeconds -= deltaTime;
            UpdateView(projectile);
            if (!projectile.IsCompleted && trajectory != null &&
                trajectory.ShouldComplete(projectile))
            {
                CompleteExpired(projectile);
                return;
            }

            if (!projectile.IsCompleted && projectile.RemainingLifetimeSeconds <= 0f)
            {
                CompleteExpired(projectile);
                return;
            }

            if (!projectile.IsCompleted &&
                projectile.TraveledDistance >= projectile.Data.MaximumTravelDistance &&
                (trajectory == null || !trajectory.CanContinueBeyondTravelLimit(projectile)))
            {
                CompleteExpired(projectile);
                return;
            }

            if (!projectile.IsCompleted && worldRangeExpired)
            {
                CompleteExpired(projectile);
            }
        }

        /// <summary>在球面边界结束普通弹体，并令回旋镖开始返程。</summary>
        private bool UpdateWorldRangeBoundary(
            ProjectileState projectile,
            TrajectoryDefinitionSO trajectory)
        {
            if (!ProjectileLaunchPattern.IsAtWorldRangeBoundary(
                    projectile.Position,
                    projectile.RangeOriginPosition,
                    projectile.Data.MaximumTravelDistance))
            {
                return false;
            }

            if (trajectory is BoomerangTrajectoryDefinitionSO && !projectile.IsReturning)
            {
                projectile.TraveledDistance = Mathf.Max(
                    projectile.TraveledDistance,
                    projectile.Data.MaximumTravelDistance);
                projectile.Direction = trajectory.GetDirection(
                    projectile,
                    projectile.Direction,
                    _spatialQueryService).normalized;
            }

            return trajectory == null || !trajectory.CanContinueBeyondTravelLimit(projectile);
        }

        /// <summary>标记投射物过期并分发过期效果。</summary>
        /// <param name="projectile">已达到距离或时间上限的投射物。</param>
        private void CompleteExpired(ProjectileState projectile)
        {
            if (projectile.IsCompleted)
            {
                return;
            }

            projectile.IsCompleted = true;
            _hitResolver.DispatchExpiredEffects(
                projectile.Data.ProjectileProfileSnapshot,
                projectile.Position);
        }

        /// <summary>将投射物状态同步到其视觉实例。</summary>
        /// <param name="projectile">包含最新位置和方向的投射物。</param>
        private static void UpdateView(ProjectileState projectile)
        {
            if (projectile.ViewInstance != null)
            {
                projectile.ViewInstance.transform.SetPositionAndRotation(
                    projectile.Position,
                    Quaternion.LookRotation(projectile.Direction, Vector3.up));

                if (projectile.Data.ProjectileProfileSnapshot?.TrajectoryDefinition is
                    LaserTrajectoryDefinitionSO)
                {
                    LaserBeamView beamView = projectile.ViewInstance
                        .GetComponentInChildren<LaserBeamView>(true);
                    beamView?.SetBeam(projectile.Data.OriginPosition, projectile.Position);
                }
            }
        }
    }
}
