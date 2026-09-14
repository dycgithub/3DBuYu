using System;
using System.Collections.Generic;
using Interfaces;
using Services;
using UnityEngine;
using VContainer.Unity;

namespace CombatSystem
{
    /// <summary>
    /// 驱动投射物移动、碰撞、伤害提交和视觉对象回收。
    /// </summary>
    public sealed class ProjectileSimulationService : ITickable, IDisposable, IProjectileHitEventSource
    {
        private readonly ProjectileViewPool _viewPool;
        private readonly ProjectileRuntimePool _runtimePool;
        private readonly IProjectileCollisionQuery _collisionQuery;
        private readonly IDamageRequestProcessor _damageProcessor;
        private readonly IProjectileEffectDispatcher _effectDispatcher;
        private readonly ISpatialQueryService _spatialQueryService;
        private readonly List<ProjectileRuntime> _activeProjectiles = new();
        private readonly List<ProjectileCollision> _collisionResults = new();
        private int _nextProjectileInstanceId;

        /// <summary>当前正在飞行的投射物数量。</summary>
        public int ActiveProjectileCount => _activeProjectiles.Count;

        /// <summary>投射物完成统一伤害处理后的命中事件。</summary>
        public event Action<ProjectileHitContext> HitProcessed;

        /// <summary>创建投射物模拟服务。</summary>
        /// <param name="viewPool">管理投射物视觉实例的对象池。</param>
        /// <param name="runtimePool">管理投射物运行时状态的对象池。</param>
        /// <param name="collisionQuery">查询投射物路径碰撞的服务。</param>
        /// <param name="damageProcessor">提交统一伤害请求的处理器。</param>
        /// <param name="effectDispatcher">分发投射物生命周期效果的服务。</param>
        /// <param name="spatialQueryService">为追踪轨迹提供目标查询的服务。</param>
        public ProjectileSimulationService(
            ProjectileViewPool viewPool,
            ProjectileRuntimePool runtimePool,
            IProjectileCollisionQuery collisionQuery,
            IDamageRequestProcessor damageProcessor,
            IProjectileEffectDispatcher effectDispatcher,
            ISpatialQueryService spatialQueryService = null)
        {
            _viewPool = viewPool;
            _runtimePool = runtimePool;
            _collisionQuery = collisionQuery;
            _damageProcessor = damageProcessor;
            _effectDispatcher = effectDispatcher;
            _spatialQueryService = spatialQueryService;
        }

        /// <summary>尝试生成一个投射物。</summary>
        /// <param name="projectileData">待生成的投射物数据。</param>
        /// <returns>生成请求被接受时返回 <c>true</c>。</returns>
        public bool TrySpawnProjectile(in ProjectileData projectileData) =>
            TrySpawnProjectileBatch(in projectileData, 1);

        /// <summary>尝试生成指定数量的投射物。</summary>
        /// <param name="projectileData">每个投射物使用的数据模板。</param>
        /// <param name="projectileCount">要生成的数量。</param>
        /// <returns>生成请求被接受时返回 <c>true</c>。</returns>
        public bool TrySpawnProjectileBatch(in ProjectileData projectileData, int projectileCount)
        {
            if (projectileCount <= 0
                || projectileData.ProjectileProfile == null
                || projectileData.DamageAmount < 0f)
            {
                return false;
            }

            for (int i = 0; i < projectileCount; i++)
            {
                SpawnSingleProjectile(in projectileData);
            }
            return true;
        }

        /// <summary>规范化方向，并创建即时或持续模拟的单个投射物。</summary>
        /// <param name="projectileData">待生成的投射物数据模板。</param>
        private void SpawnSingleProjectile(in ProjectileData projectileData)
        {
            Vector3 launchDirection = projectileData.LaunchDirection.sqrMagnitude > 0.0001f
                ? projectileData.LaunchDirection.normalized
                : Vector3.forward;
            ProjectileData normalizedData = projectileData;
            normalizedData.ProjectileInstanceId = ++_nextProjectileInstanceId;
            normalizedData.LaunchDirection = launchDirection;
            normalizedData.ProjectileProfileSnapshot = CreateProfileSnapshot(
                normalizedData.ProjectileProfile);
            ProjectileProfileSO profile = normalizedData.ProjectileProfileSnapshot;
            normalizedData.ProjectileProfile = profile;
            profile.TrajectoryDefinition?.ConfigureData(ref normalizedData);

            if (profile.TrajectoryDefinition != null && profile.TrajectoryDefinition.IsHitscan)
            {
                ResolveHitscanProjectile(normalizedData);
                DestroyProfileSnapshot(profile);
                return;
            }

            if (normalizedData.MaximumTravelDistance <= 0f)
            {
                _effectDispatcher?.DispatchExpiredEffects(
                    profile,
                    normalizedData.OriginPosition);
                DestroyProfileSnapshot(profile);
                return;
            }

            GameObject viewInstance = _viewPool?.RentProjectileView(
                profile,
                normalizedData.OriginPosition,
                Quaternion.LookRotation(launchDirection, Vector3.up));
            ProjectileRuntime runtime = _runtimePool.Rent(normalizedData, viewInstance);
            profile.TrajectoryDefinition?.InitializeRuntime(runtime, _spatialQueryService);
            _activeProjectiles.Add(runtime);
        }

        /// <summary>由 VContainer 每帧推进投射物模拟。</summary>
        public void Tick() => SimulateStep(Time.deltaTime);

        /// <summary>按指定时间步推进所有活动投射物。</summary>
        /// <param name="deltaTime">本次模拟经过的秒数。</param>
        public void SimulateStep(float deltaTime)
        {
            deltaTime = Mathf.Max(0f, deltaTime);
            // 反向遍历允许在同一帧释放并移除已完成对象而不跳过下一个元素。
            for (int i = _activeProjectiles.Count - 1; i >= 0; i--)
            {
                ProjectileRuntime projectile = _activeProjectiles[i];
                SimulateProjectile(projectile, deltaTime);
                if (!projectile.IsCompleted)
                {
                    continue;
                }

                ReleaseProjectile(projectile);
                _activeProjectiles.RemoveAt(i);
            }
        }

        /// <summary>释放所有活动投射物和对象池资源。</summary>
        public void Dispose()
        {
            for (int i = 0; i < _activeProjectiles.Count; i++)
            {
                ReleaseProjectile(_activeProjectiles[i]);
            }

            _activeProjectiles.Clear();
            _viewPool?.ClearAllProjectileViews();
            _runtimePool?.ClearAvailableRuntimes();
        }

        /// <summary>推进一个投射物，处理路径碰撞、伤害和过期状态。</summary>
        /// <param name="projectile">待推进的投射物运行时状态。</param>
        /// <param name="deltaTime">本次模拟经过的秒数。</param>
        private void SimulateProjectile(ProjectileRuntime projectile, float deltaTime)
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
            Vector3 direction = trajectory != null
                ? trajectory.GetDirection(projectile, projectile.Direction).normalized
                : projectile.Direction;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.forward;
            }

            projectile.Direction = direction;
            float remainingDistance = Mathf.Max(
                0f,
                projectile.Data.MaximumTravelDistance - projectile.TraveledDistance);
            bool canContinueBeyondTravelLimit = trajectory != null &&
                trajectory.CanContinueBeyondTravelLimit(projectile);
            if (remainingDistance <= 0f && !canContinueBeyondTravelLimit)
            {
                CompleteExpiredProjectile(projectile);
                return;
            }

            float travelDistance = projectile.Data.SpeedUnitsPerSecond * deltaTime;
            if (!canContinueBeyondTravelLimit)
            {
                travelDistance = Mathf.Min(travelDistance, remainingDistance);
            }

            if (travelDistance <= 0f)
            {
                projectile.RemainingLifetimeSeconds -= deltaTime;
                if (projectile.RemainingLifetimeSeconds <= 0f)
                {
                    CompleteExpiredProjectile(projectile);
                }
                return;
            }

            // 使用扫掠而不是只检测终点，避免高速投射物穿过细小碰撞体。
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
                        ApplyProjectileHit(
                            projectile,
                            collision.Target,
                            collision.Hit.point,
                            collision.Hit.normal);
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
                ApplyProjectileHit(projectile, target, hit.point, hit.normal);
                hasCollision = true;
            }

            if (!hasCollision)
            {
                projectile.Position += direction * travelDistance;
                projectile.TraveledDistance += travelDistance;
            }

            if (hasCollision && !projectile.IsCompleted)
            {
                if (trajectory != null && trajectory.ContinuesAfterHit)
                {
                    float remainingAfterHit = Mathf.Max(0f, travelDistance - firstCollisionDistance);
                    projectile.Position += direction * remainingAfterHit;
                    projectile.TraveledDistance += remainingAfterHit;
                }
                else
                {
                    // 穿透轨迹后偏移一点距离，避免下一帧停在同一碰撞面上。
                    projectile.Position += direction * 0.01f;
                }
            }

            projectile.RemainingLifetimeSeconds -= deltaTime;
            UpdateProjectileView(projectile);
            if (!projectile.IsCompleted && trajectory != null &&
                trajectory.ShouldComplete(projectile))
            {
                CompleteExpiredProjectile(projectile);
                return;
            }

            if (!projectile.IsCompleted &&
                projectile.RemainingLifetimeSeconds <= 0f)
            {
                CompleteExpiredProjectile(projectile);
                return;
            }

            if (!projectile.IsCompleted &&
                projectile.TraveledDistance >= projectile.Data.MaximumTravelDistance &&
                (trajectory == null || !trajectory.CanContinueBeyondTravelLimit(projectile)))
            {
                CompleteExpiredProjectile(projectile);
            }
        }

        /// <summary>执行 Hitscan 射线查询并提交一次命中伤害。</summary>
        /// <param name="projectileData">包含射线起点、方向和伤害数据的投射物。</param>
        private void ResolveHitscanProjectile(ProjectileData projectileData)
        {
            if (_collisionQuery == null
                || !_collisionQuery.TryFindRaycastCollision(
                    projectileData,
                    out RaycastHit hit,
                    out IDamageable target))
            {
                _effectDispatcher?.DispatchExpiredEffects(
                    projectileData.ProjectileProfileSnapshot,
                    projectileData.OriginPosition +
                    projectileData.LaunchDirection * projectileData.MaximumTravelDistance);
                return;
            }

            SubmitHit(
                projectileData,
                projectileData.ProjectileProfileSnapshot,
                target,
                hit.point,
                hit.normal);
        }

        /// <summary>处理一次投射物命中，包含重复目标过滤和穿透消耗。</summary>
        /// <param name="projectile">发生命中的投射物运行时状态。</param>
        /// <param name="target">命中的伤害目标。</param>
        /// <param name="hitPosition">命中位置。</param>
        /// <param name="hitNormal">命中表面法线。</param>
        private void ApplyProjectileHit(
            ProjectileRuntime projectile,
            IDamageable target,
            Vector3 hitPosition,
            Vector3 hitNormal)
        {
            if (target == null)
            {
                return;
            }

            int targetInstanceId = target.Transform != null
                ? target.Transform.GetInstanceID()
                : 0;
            if (targetInstanceId != 0 &&
                !projectile.HitTargetInstanceIds.Add(targetInstanceId))
            {
                return;
            }

            SubmitHit(
                projectile.Data,
                projectile.Data.ProjectileProfileSnapshot,
                target,
                hitPosition,
                hitNormal,
                projectile);

            TrajectoryDefinitionSO trajectory = projectile.Data.ProjectileProfileSnapshot != null
                ? projectile.Data.ProjectileProfileSnapshot.TrajectoryDefinition
                : null;
            if (trajectory != null && trajectory.ContinuesAfterHit)
            {
                return;
            }

            if (projectile.RemainingPenetrationCount > 0)
            {
                projectile.RemainingPenetrationCount--;
                return;
            }

            projectile.IsCompleted = true;
        }

        /// <summary>使用普通投射物和 Hitscan 共用的命中提交链。</summary>
        /// <param name="projectileData">投射物生成时的伤害数据快照。</param>
        /// <param name="profile">投射物生成时的配置快照。</param>
        /// <param name="target">命中的伤害目标。</param>
        /// <param name="hitPosition">命中位置。</param>
        /// <param name="hitNormal">命中法线。</param>
        private void SubmitHit(
            ProjectileData projectileData,
            ProjectileProfileSO profile,
            IDamageable target,
            Vector3 hitPosition,
            Vector3 hitNormal,
            ProjectileRuntime projectile = null)
        {
            Vector3 impactDirection = projectile != null
                ? projectile.Direction
                : projectileData.LaunchDirection;
            DamageRequest request = CreateDamageRequest(
                projectileData,
                hitPosition,
                hitNormal,
                impactDirection);
            _effectDispatcher?.DispatchBeforeHitEffects(
                profile,
                request,
                target);
            DamageResult result = default;
            _damageProcessor?.TryApplyDamage(target, request, out result);
            if (_effectDispatcher is IProjectileStateEffectDispatcher stateEffectDispatcher)
            {
                stateEffectDispatcher.DispatchHitEffects(
                    projectile,
                    profile,
                    request,
                    result,
                    target);
            }
            else
            {
                _effectDispatcher?.DispatchHitEffects(
                    profile,
                    request,
                    result,
                    target);
            }
            HitProcessed?.Invoke(new ProjectileHitContext(
                projectileData,
                target,
                hitPosition,
                hitNormal,
                result));
        }

        /// <summary>将投射物数据和物理命中信息转换为伤害请求。</summary>
        /// <param name="projectileData">投射物伤害数据。</param>
        /// <param name="hitPosition">命中位置。</param>
        /// <param name="hitNormal">命中法线。</param>
        /// <returns>统一伤害管线使用的请求。</returns>
        private static DamageRequest CreateDamageRequest(
            ProjectileData projectileData,
            Vector3 hitPosition,
            Vector3 hitNormal,
            Vector3 impactDirection)
        {
            return new DamageRequest
            {
                AttackInstanceId = projectileData.AttackInstanceId,
                SourceInstanceId = projectileData.SourceInstanceId,
                DamageAmount = projectileData.DamageAmount,
                DamageType = projectileData.DamageType,
                HitPosition = hitPosition,
                HitNormal = hitNormal,
                ImpactDirection = impactDirection,
                WasCritical = projectileData.WasCritical
            };
        }

        /// <summary>标记投射物过期并分发过期效果。</summary>
        /// <param name="projectile">已达到距离或时间上限的投射物。</param>
        private void CompleteExpiredProjectile(ProjectileRuntime projectile)
        {
            if (projectile.IsCompleted)
            {
                return;
            }

            projectile.IsCompleted = true;
            _effectDispatcher?.DispatchExpiredEffects(
                projectile.Data.ProjectileProfileSnapshot,
                projectile.Position);
        }

        /// <summary>将运行时位置和方向同步到视觉实例。</summary>
        /// <param name="projectile">包含最新变换的投射物。</param>
        private static void UpdateProjectileView(ProjectileRuntime projectile)
        {
            if (projectile.ViewInstance != null)
            {
                projectile.ViewInstance.transform.SetPositionAndRotation(
                    projectile.Position,
                    Quaternion.LookRotation(projectile.Direction, Vector3.up));
            }
        }

        /// <summary>归还投射物视觉实例和运行时状态。</summary>
        /// <param name="projectile">待释放的投射物。</param>
        private void ReleaseProjectile(ProjectileRuntime projectile)
        {
            _viewPool?.ReturnProjectileView(projectile.ViewInstance);
            DestroyProfileSnapshot(projectile.Data.ProjectileProfileSnapshot);
            _runtimePool?.Return(projectile);
        }

        /// <summary>复制投射物配置，使飞行期间不再读取原始资产的可变字段。</summary>
        /// <param name="profile">攻击创建时使用的配置资产。</param>
        /// <returns>独立的配置快照。</returns>
        private static ProjectileProfileSO CreateProfileSnapshot(ProjectileProfileSO profile)
        {
            return profile != null ? UnityEngine.Object.Instantiate(profile) : null;
        }

        /// <summary>释放投射物持有的临时配置快照。</summary>
        /// <param name="profile">待释放的配置快照。</param>
        private static void DestroyProfileSnapshot(ProjectileProfileSO profile)
        {
            if (profile == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(profile);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }
    }
}
