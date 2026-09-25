using System;
using System.Collections.Generic;
using Interfaces;
using Services;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

namespace CombatSystem
{
    /// <summary>
    /// 驱动投射物移动、碰撞、伤害提交和视觉对象回收。
    /// </summary>
    public sealed class ProjectileSimulationService : ITickable, IDisposable, IProjectileHitEventSource
    {
        private const float EmissionTimeEpsilon = 0.000001f;
        private static readonly ProfilerMarker TickMarker =
            new("3DBuYu/VContainer/ProjectileSimulationService.Tick");
        private static readonly ProfilerMarker SimulateStepMarker =
            new("3DBuYu/ProjectileSimulationService.SimulateStep");
        private static readonly ProfilerMarker PublishSpatialSnapshotMarker =
            new("3DBuYu/ProjectileSimulationService.PublishSpatialSnapshot");

        private readonly ProjectileViewPool _viewPool;
        private readonly ProjectileRuntimePool _runtimePool;
        private readonly IProjectileCollisionQuery _collisionQuery;
        private readonly ProjectileHitResolver _hitResolver;
        private readonly ISpatialQueryService _spatialQueryService;
        // 快照发布与查询服务共用同一实例，不再为内部能力单独占用容器接口绑定。
        private readonly IProjectileSnapshotPublisher _projectileSnapshotPublisher;
        private readonly List<ProjectileRuntime> _activeProjectiles = new();
        private readonly List<ProjectileRuntime> _pendingProjectiles = new();
        private readonly List<PendingProjectileEmission> _pendingEmissions = new();
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
        /// <param name="spatialQueryService">提供目标查询并可接收活动投射物空间快照的服务。</param>
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
            _spatialQueryService = spatialQueryService;
            _projectileSnapshotPublisher = spatialQueryService as IProjectileSnapshotPublisher;
            _hitResolver = new ProjectileHitResolver(
                damageProcessor,
                effectDispatcher,
                spatialQueryService,
                QueueSplitProjectile,
                context => HitProcessed?.Invoke(context));
        }

        /// <summary>尝试生成一个投射物。</summary>
        /// <param name="projectileData">待生成的投射物数据。</param>
        /// <returns>生成请求被接受时返回 <c>true</c>。</returns>
        public bool TrySpawnProjectile(in ProjectileData projectileData) =>
            TrySpawnProjectileBatch(in projectileData, 1);

        /// <summary>按配置生成一批投射物，并为批次应用对称散射。</summary>
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

            ProjectileProfileSO profile = projectileData.ProjectileProfile;
            int resolvedCount = profile.EmitProjectilesSequentially &&
                profile.SequentialProjectileCount > 0
                ? profile.SequentialProjectileCount
                : projectileCount;
            if (profile.EmitProjectilesSequentially && resolvedCount > 1 &&
                projectileData.AttacksPerSecond > 0f)
            {
                return TryScheduleProjectileBatch(in projectileData, resolvedCount);
            }

            float scatterAngle = profile.ScatterAngleDegrees;
            for (int index = 0; index < resolvedCount; index++)
            {
                ProjectileData launchData = CreateLaunchData(
                    in projectileData,
                    resolvedCount,
                    index,
                    scatterAngle);
                ProjectileRuntime projectile = SpawnSingleProjectile(in launchData);
                if (projectile != null)
                {
                    _activeProjectiles.Add(projectile);
                }
            }
            PublishSpatialSnapshot();
            return true;
        }

        /// <summary>立即发射第一颗，并把余下投射物均匀排入一个攻击间隔。</summary>
        private bool TryScheduleProjectileBatch(in ProjectileData projectileData, int projectileCount)
        {
            ProjectileProfileSO profileSnapshot = CreateProfileSnapshot(projectileData.ProjectileProfile);
            if (profileSnapshot == null)
            {
                return false;
            }

            ProjectileData template = projectileData;
            template.ProjectileProfile = profileSnapshot;
            ProjectileData firstData = template;
            firstData.LaunchDirection = ProjectileLaunchPattern.GetRandomDirection(
                projectileData.LaunchDirection,
                profileSnapshot.RandomDirectionAngleDegrees);
            ProjectileRuntime firstProjectile = SpawnSingleProjectile(in firstData);
            if (firstProjectile == null)
            {
                DestroyProfileSnapshot(profileSnapshot);
                return false;
            }

            _activeProjectiles.Add(firstProjectile);
            float attackInterval = 1f / Mathf.Max(0.001f, projectileData.AttacksPerSecond);
            _pendingEmissions.Add(new PendingProjectileEmission(
                template,
                profileSnapshot,
                projectileCount,
                attackInterval / (projectileCount - 1)));
            PublishSpatialSnapshot();
            return true;
        }

        /// <summary>推进各攻击批次的后续投射物。</summary>
        private void AdvancePendingEmissions(float deltaTime)
        {
            for (int index = _pendingEmissions.Count - 1; index >= 0; index--)
            {
                PendingProjectileEmission emission = _pendingEmissions[index];
                emission.SecondsUntilNextShot -= deltaTime;
                while (emission.SecondsUntilNextShot <= EmissionTimeEpsilon &&
                    emission.NextProjectileIndex < emission.ProjectileCount)
                {
                    ProjectileData launchData = emission.Template;
                    launchData.LaunchDirection = ProjectileLaunchPattern.GetRandomDirection(
                        emission.Template.LaunchDirection,
                        emission.ProfileSnapshot.RandomDirectionAngleDegrees);
                    ProjectileRuntime projectile = SpawnSingleProjectile(in launchData);
                    if (projectile != null)
                    {
                        _activeProjectiles.Add(projectile);
                    }

                    emission.NextProjectileIndex++;
                    emission.SecondsUntilNextShot += emission.SecondsBetweenShots;
                }

                if (emission.NextProjectileIndex >= emission.ProjectileCount)
                {
                    DestroyProfileSnapshot(emission.ProfileSnapshot);
                    _pendingEmissions.RemoveAt(index);
                }
                else
                {
                    _pendingEmissions[index] = emission;
                }
            }
        }

        /// <summary>复制投射物模板并按批次配置确定单发方向。</summary>
        private static ProjectileData CreateLaunchData(
            in ProjectileData projectileData,
            int projectileCount,
            int projectileIndex,
            float scatterAngle)
        {
            ProjectileData launchData = projectileData;
            launchData.LaunchDirection = ProjectileLaunchPattern.GetDirection(
                projectileData.LaunchDirection,
                projectileCount,
                projectileIndex,
                scatterAngle);
            if (projectileData.ProjectileProfile.RandomDirectionAngleDegrees > 0f)
            {
                launchData.LaunchDirection = ProjectileLaunchPattern.GetRandomDirection(
                    launchData.LaunchDirection,
                    projectileData.ProjectileProfile.RandomDirectionAngleDegrees);
            }

            return launchData;
        }

        /// <summary>规范化单发数据、只应用一次速度变化，并初始化指定追踪目标。</summary>
        /// <param name="projectileData">待生成的投射物数据模板。</param>
        /// <param name='trackingTarget'>分裂子弹选中的追踪目标；普通子弹不指定。</param>
        private ProjectileRuntime SpawnSingleProjectile(in ProjectileData projectileData, IDamageable trackingTarget = null)
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
                return null;
            }

            if (normalizedData.MaximumTravelDistance <= 0f)
            {
                _hitResolver.DispatchExpiredEffects(
                    profile,
                    normalizedData.OriginPosition);
                DestroyProfileSnapshot(profile);
                return null;
            }

            if (!normalizedData.HasAppliedSpeedMultiplier)
            {
                normalizedData.SpeedUnitsPerSecond *= Mathf.Max(
                    0f,
                    profile.ProjectileSpeedMultiplier);
                normalizedData.HasAppliedSpeedMultiplier = true;
            }

            if (!normalizedData.HasAppliedSpeedVariance)
            {
                normalizedData.SpeedUnitsPerSecond = ProjectileLaunchPattern.GetSpeed(
                    normalizedData.SpeedUnitsPerSecond,
                    profile.RandomSpeedVariance);
                normalizedData.HasAppliedSpeedVariance = true;
            }

            GameObject viewInstance = _viewPool?.RentProjectileView(
                profile,
                normalizedData.OriginPosition,
                Quaternion.LookRotation(launchDirection, Vector3.up));
            ProjectileRuntime runtime = _runtimePool.Rent(normalizedData, viewInstance);
            if (trackingTarget != null && trackingTarget.IsAlive)
            {
                runtime.TrackingTarget = trackingTarget;
            }

            profile.TrajectoryDefinition?.InitializeRuntime(runtime, _spatialQueryService);
            return runtime;
        }

        /// <summary>由 VContainer 每帧推进投射物模拟。</summary>
        public void Tick()
        {
            using var profilerScope = TickMarker.Auto();
            SimulateStep(Time.deltaTime);
        }

        /// <summary>按指定时间步推进所有活动投射物。</summary>
        /// <param name="deltaTime">本次模拟经过的秒数。</param>
        public void SimulateStep(float deltaTime)
        {
            using var profilerScope = SimulateStepMarker.Auto();
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

            _activeProjectiles.AddRange(_pendingProjectiles);
            _pendingProjectiles.Clear();
            AdvancePendingEmissions(deltaTime);

            PublishSpatialSnapshot();
        }

        /// <summary>完整模拟步结束后批量发布分类索引，物理扫掠仍由碰撞服务执行。</summary>
        private void PublishSpatialSnapshot()
        {
            using var profilerScope = PublishSpatialSnapshotMarker.Auto();
            _projectileSnapshotPublisher?.PublishProjectiles(_activeProjectiles);
        }

        /// <summary>释放所有活动投射物和对象池资源。</summary>
        public void Dispose()
        {
            for (int i = 0; i < _activeProjectiles.Count; i++)
            {
                ReleaseProjectile(_activeProjectiles[i]);
            }

            _activeProjectiles.Clear();
            for (int i = 0; i < _pendingProjectiles.Count; i++)
            {
                ReleaseProjectile(_pendingProjectiles[i]);
            }

            _pendingProjectiles.Clear();
            for (int index = 0; index < _pendingEmissions.Count; index++)
            {
                DestroyProfileSnapshot(_pendingEmissions[index].ProfileSnapshot);
            }
            _pendingEmissions.Clear();
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

            float travelDistance = projectile.Data.SpeedUnitsPerSecond * deltaTime +
                projectile.PendingTravelDistance;
            projectile.PendingTravelDistance = 0f;
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
                _hitResolver.DispatchExpiredEffects(
                    projectileData.ProjectileProfileSnapshot,
                    projectileData.OriginPosition +
                    projectileData.LaunchDirection * projectileData.MaximumTravelDistance);
                return;
            }

            _hitResolver.SubmitHitscan(
                projectileData,
                projectileData.ProjectileProfileSnapshot,
                target,
                hit.point,
                hit.normal);
        }

        /// <summary>将碰撞结果交给命中处理器，保持模拟循环只负责运动与碰撞。</summary>
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
            _hitResolver.ApplyProjectileHit(projectile, target, hitPosition, hitNormal);
        }

        /// <summary>为分裂子弹复制命中记录、设置目标并排队，避免修改正在遍历的活动列表。</summary>
        private void QueueSplitProjectile(
            ProjectileData projectileData,
            ProjectileRuntime source,
            IDamageable trackingTarget)
        {
            ProjectileRuntime child = SpawnSingleProjectile(in projectileData, trackingTarget);
            if (child == null)
            {
                return;
            }

            foreach (int targetInstanceId in source.HitTargetInstanceIds)
            {
                child.HitTargetInstanceIds.Add(targetInstanceId);
            }

            _pendingProjectiles.Add(child);
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
            _hitResolver.DispatchExpiredEffects(
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

        private struct PendingProjectileEmission
        {
            public readonly ProjectileData Template;
            public readonly ProjectileProfileSO ProfileSnapshot;
            public readonly int ProjectileCount;
            public readonly float SecondsBetweenShots;
            public int NextProjectileIndex;
            public float SecondsUntilNextShot;

            public PendingProjectileEmission(
                ProjectileData template,
                ProjectileProfileSO profileSnapshot,
                int projectileCount,
                float secondsBetweenShots)
            {
                Template = template;
                ProfileSnapshot = profileSnapshot;
                ProjectileCount = projectileCount;
                SecondsBetweenShots = secondsBetweenShots;
                NextProjectileIndex = 1;
                SecondsUntilNextShot = secondsBetweenShots;
            }
        }
    }
}
