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
    /// 驱动投射物移动、碰撞和回收；整卡发射先准备完整批次，再发布活动状态。
    /// </summary>
    public sealed partial class ProjectileSimulationService : ITickable, IDisposable, IProjectileHitEventSource
    {
        private const float EmissionTimeEpsilon = 0.000001f;
        private static readonly ProfilerMarker TickMarker =
            new("3DBuYu/VContainer/ProjectileSimulationService.Tick");
        private static readonly ProfilerMarker SimulateStepMarker =
            new("3DBuYu/ProjectileSimulationService.SimulateStep");
        private static readonly ProfilerMarker PublishSpatialSnapshotMarker =
            new("3DBuYu/ProjectileSimulationService.PublishSpatialSnapshot");

        private readonly ProjectileViewPool _viewPool;
        private readonly ProjectileStatePool _statePool;
        private readonly IProjectileCollisionQuery _collisionQuery;
        private readonly ProjectileHitResolver _hitResolver;
        private readonly ProjectileStepper _stepper;
        private readonly ISpatialQueryService _spatialQueryService;
        // 快照发布与查询服务共用同一实例，不再为内部能力单独占用容器接口绑定。
        private readonly IProjectileSnapshotPublisher _projectileSnapshotPublisher;
        private readonly List<ProjectileState> _activeProjectiles = new();
        private readonly List<ProjectileState> _pendingProjectiles = new();
        private readonly List<PendingProjectileEmission> _pendingEmissions = new();
        private int _nextProjectileInstanceId;

        /// <summary>当前正在飞行的投射物数量。</summary>
        public int ActiveProjectileCount => _activeProjectiles.Count;

        /// <summary>投射物完成统一伤害处理后的命中事件。</summary>
        public event Action<ProjectileHitContext> HitProcessed;

        /// <summary>创建投射物模拟服务。</summary>
        /// <param name="viewPool">管理投射物视觉实例的对象池。</param>
        /// <param name="statePool">管理投射物状态的对象池。</param>
        /// <param name="collisionQuery">查询投射物路径碰撞的服务。</param>
        /// <param name="damageProcessor">提交统一伤害请求的处理器。</param>
        /// <param name="effectDispatcher">分发投射物生命周期效果的服务。</param>
        /// <param name="spatialQueryService">提供目标查询并可接收活动投射物空间快照的服务。</param>
        public ProjectileSimulationService(
            ProjectileViewPool viewPool,
            ProjectileStatePool statePool,
            IProjectileCollisionQuery collisionQuery,
            IDamageRequestProcessor damageProcessor,
            IProjectileEffectDispatcher effectDispatcher,
            ISpatialQueryService spatialQueryService = null)
        {
            _viewPool = viewPool;
            _statePool = statePool;
            _collisionQuery = collisionQuery;
            _spatialQueryService = spatialQueryService;
            _projectileSnapshotPublisher = spatialQueryService as IProjectileSnapshotPublisher;
            _hitResolver = new ProjectileHitResolver(
                damageProcessor,
                effectDispatcher,
                spatialQueryService,
                QueueSplitProjectile,
                context => HitProcessed?.Invoke(context));
            _stepper = new ProjectileStepper(
                _collisionQuery,
                _hitResolver,
                _spatialQueryService);
        }

        /// <summary>尝试生成一个投射物。</summary>
        /// <param name="projectileData">待生成的投射物数据。</param>
        /// <returns>生成请求被接受时返回 <c>true</c>。</returns>
        public bool TrySpawnProjectile(in ProjectileData projectileData) =>
            TrySpawnProjectileBatch(in projectileData, 1);

        /// <summary>按配置生成一批投射物，并应用对称起点间距和方向散射。</summary>
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
                ProjectileState projectile = SpawnSingleProjectile(in launchData);
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
            ProjectileData firstData = CreateLaunchData(
                in template,
                projectileCount,
                0,
                0f);
            ProjectileState firstProjectile = SpawnSingleProjectile(in firstData);
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
                    ProjectileData launchData = CreateLaunchData(
                        in emission.Template,
                        emission.ProjectileCount,
                        emission.NextProjectileIndex,
                        0f);
                    ProjectileState projectile = SpawnSingleProjectile(in launchData);
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

        /// <summary>复制模板、计算横向起点，并优先从新起点瞄准显式目标。</summary>
        private static ProjectileData CreateLaunchData(
            in ProjectileData projectileData,
            int projectileCount,
            int projectileIndex,
            float scatterAngle)
        {
            ProjectileData launchData = projectileData;
            launchData.OriginPosition = ProjectileLaunchPattern.GetOriginPosition(
                projectileData.OriginPosition,
                projectileData.LaunchDirection,
                projectileCount,
                projectileIndex,
                ProjectileLaunchPattern.GetMinimumSpacing(
                    projectileData.ProjectileProfile,
                    projectileData.CollisionRadius));

            if (projectileData.TrackingTarget.IsAliveAndValid())
            {
                Vector3 targetDirection = projectileData.TrackingTarget.Position - launchData.OriginPosition;
                launchData.LaunchDirection = targetDirection.sqrMagnitude > 0.0001f
                    ? targetDirection.normalized
                    : projectileData.LaunchDirection.normalized;
                return launchData;
            }

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

        /// <summary>创建普通投射物并保留显式追踪目标。</summary>
        /// <param name="projectileData">待生成的投射物数据模板。</param>
        private ProjectileState SpawnSingleProjectile(
            in ProjectileData projectileData,
            IDamageable trackingTarget = null)
        {
            return SpawnProjectile(in projectileData, trackingTarget, null);
        }

        /// <summary>创建分裂投射物并在轨迹初始化前复制父弹的命中记录。</summary>
        /// <param name="projectileData">分裂子弹数据模板。</param>
        /// <param name="trackingTarget">分配给子弹的次级追踪目标。</param>
        /// <param name="splitSource">提供已命中目标记录的父弹。</param>
        private ProjectileState SpawnSplitProjectile(
            in ProjectileData projectileData,
            IDamageable trackingTarget,
            ProjectileState splitSource)
        {
            return SpawnProjectile(in projectileData, trackingTarget, splitSource);
        }

        private ProjectileState SpawnProjectile(
            in ProjectileData projectileData,
            IDamageable trackingTarget,
            ProjectileState splitSource)
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
            ProjectileLaunchPattern.PrepareWorldRange(ref normalizedData, launchDirection);

            if (profile.TrajectoryDefinition != null && profile.TrajectoryDefinition.IsHitscan)
            {
                ProjectileLaunchPattern.LimitRayToWorldRange(
                    ref normalizedData,
                    launchDirection);
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
            ProjectileState state = _statePool.Rent(normalizedData, viewInstance);
            if (splitSource != null)
            {
                foreach (int targetInstanceId in splitSource.HitTargetInstanceIds)
                {
                    state.HitTargetInstanceIds.Add(targetInstanceId);
                }
            }

            IDamageable selectedTarget = trackingTarget.IsAliveAndValid()
                ? trackingTarget
                : normalizedData.TrackingTarget;
            if (selectedTarget.IsAliveAndValid())
            {
                state.TrackingTarget = selectedTarget;
            }

            profile.TrajectoryDefinition?.InitializeRuntime(state, _spatialQueryService);
            return state;
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
            for (int index = _activeProjectiles.Count - 1; index >= 0; index--)
            {
                ProjectileState projectile = _activeProjectiles[index];
                _stepper.Simulate(projectile, deltaTime);
                if (!projectile.IsCompleted)
                {
                    continue;
                }

                ReleaseProjectile(projectile);
                _activeProjectiles.RemoveAt(index);
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
            _statePool?.ClearAvailableStates();
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

        /// <summary>为分裂子弹复制命中记录、设置目标并排队，避免修改正在遍历的活动列表。</summary>
        private void QueueSplitProjectile(
            ProjectileData projectileData,
            ProjectileState source,
            IDamageable trackingTarget)
        {
            ProjectileState child = SpawnSplitProjectile(
                in projectileData,
                trackingTarget,
                source);
            if (child == null)
            {
                return;
            }

            _pendingProjectiles.Add(child);
        }

        /// <summary>归还投射物视觉实例和状态对象。</summary>
        /// <param name="projectile">待释放的投射物。</param>
        private void ReleaseProjectile(ProjectileState projectile)
        {
            _viewPool?.ReturnProjectileView(projectile.ViewInstance);
            DestroyProfileSnapshot(projectile.Data.ProjectileProfileSnapshot);
            _statePool?.Return(projectile);
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
