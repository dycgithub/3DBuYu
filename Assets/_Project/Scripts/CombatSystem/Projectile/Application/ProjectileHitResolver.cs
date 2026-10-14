using System;
using System.Collections.Generic;
using Interfaces;
using Services;
using SpatialIndexing;
using UnityEngine;
using EnemySystem;

namespace CombatSystem
{
    /// <summary>统一提交投射物命中，并处理跳弹与分裂目标选择。</summary>
    internal sealed class ProjectileHitResolver
    {
        private readonly IDamageRequestProcessor _damageProcessor;
        private readonly IProjectileEffectDispatcher _effectDispatcher;
        private readonly ISpatialQueryService _spatialQueryService;
        private readonly Action<ProjectileData, ProjectileState, IDamageable> _queueSplitProjectile;
        private readonly Action<ProjectileHitContext> _publishHitProcessed;
        private readonly Dictionary<long, ComboHitState> _comboHits = new();
        private readonly List<long> _expiredComboKeys = new();
        private float _nextComboCleanupTime;

        /// <summary>创建命中处理器并连接伤害、空间查询及分裂生成边界。</summary>
        internal ProjectileHitResolver(
            IDamageRequestProcessor damageProcessor,
            IProjectileEffectDispatcher effectDispatcher,
            ISpatialQueryService spatialQueryService,
            Action<ProjectileData, ProjectileState, IDamageable> queueSplitProjectile,
            Action<ProjectileHitContext> publishHitProcessed)
        {
            _damageProcessor = damageProcessor;
            _effectDispatcher = effectDispatcher;
            _spatialQueryService = spatialQueryService;
            _queueSplitProjectile = queueSplitProjectile;
            _publishHitProcessed = publishHitProcessed;
        }

        /// <summary>按命中点查找附近目标，分裂生成子弹后再应用跳弹和穿透规则。</summary>
        internal void ApplyProjectileHit(
            ProjectileState projectile,
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

            Vector3 impactDirection = projectile.Direction;
            SubmitHit(
                projectile.Data,
                projectile.Data.ProjectileProfileSnapshot,
                target,
                hitPosition,
                hitNormal,
                impactDirection,
                projectile);
            TrySplitProjectile(projectile, hitPosition, impactDirection);

            if (TryRicochet(projectile, hitPosition))
            {
                return;
            }

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

        /// <summary>将 Hitscan 命中交给与普通投射物相同的伤害和效果管线。</summary>
        internal void SubmitHitscan(
            ProjectileData projectileData,
            ProjectileProfileSO profile,
            IDamageable target,
            Vector3 hitPosition,
            Vector3 hitNormal)
        {
            SubmitHit(
                projectileData,
                profile,
                target,
                hitPosition,
                hitNormal,
                projectileData.LaunchDirection,
                null);
        }

        /// <summary>分发投射物的过期效果。</summary>
        internal void DispatchExpiredEffects(ProjectileProfileSO profile, Vector3 position)
        {
            _effectDispatcher?.DispatchExpiredEffects(profile, position);
        }

        /// <summary>选择尚未命中的目标并改变弹跳方向，回旋镖返程阶段保持原轨迹。</summary>
        private bool TryRicochet(ProjectileState projectile, Vector3 hitPosition)
        {
            ProjectileProfileSO profile = projectile.Data.ProjectileProfileSnapshot;
            TrajectoryDefinitionSO trajectory = profile != null
                ? profile.TrajectoryDefinition
                : null;
            if (profile == null || projectile.RemainingRicochetCount <= 0 ||
                _spatialQueryService == null || profile.RicochetSearchRadius <= 0f ||
                (trajectory is BoomerangTrajectoryDefinitionSO && projectile.IsReturning))
            {
                return false;
            }

            List<IDamageable> targets = _spatialQueryService.QueryRadius(
                hitPosition,
                profile.RicochetSearchRadius,
                ResolveTargetLayer(projectile.Data.TargetMode));
            IDamageable target = FindNearestUnhitTarget(
                targets,
                hitPosition,
                profile.RicochetSearchRadius,
                projectile.HitTargetInstanceIds,
                null);
            if (target == null)
            {
                return false;
            }

            Vector3 direction = target.Position - hitPosition;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            projectile.RemainingRicochetCount--;
            projectile.ApplyDamageMultiplier(profile.RicochetDamageMultiplier);
            projectile.Direction = direction.normalized;
            projectile.Position = hitPosition;
            projectile.TrackingTarget = target;
            projectile.DirectionChangedThisStep = true;
            return true;
        }

        /// <summary>命中后重置分裂弹射程球心、错开起点并传递选中目标。</summary>
        private void TrySplitProjectile(
            ProjectileState projectile,
            Vector3 hitPosition,
            Vector3 impactDirection)
        {
            ProjectileProfileSO profile = projectile.Data.ProjectileProfileSnapshot;
            if (profile == null || projectile.HasSplit ||
                profile.SplitProjectileCount <= 0)
            {
                return;
            }

            projectile.HasSplit = true;
            float searchRadius = Mathf.Max(0f, profile.SplitTargetSearchRadius);
            List<IDamageable> targets = searchRadius > 0f && _spatialQueryService != null
                ? _spatialQueryService.QueryRadius(
                    hitPosition,
                    searchRadius,
                    ResolveTargetLayer(projectile.Data.TargetMode))
                : null;
            HashSet<int> selectedTargetIds = new();
            float minimumSpacing = ProjectileLaunchPattern.GetMinimumSpacing(
                profile,
                projectile.Data.CollisionRadius);

            for (int index = 0; index < profile.SplitProjectileCount; index++)
            {
                IDamageable target = FindNearestUnhitTarget(
                    targets,
                    hitPosition,
                    searchRadius,
                    projectile.HitTargetInstanceIds,
                    selectedTargetIds);
                Vector3 direction = target != null
                    ? target.Position - hitPosition
                    : ProjectileLaunchPattern.GetDirection(
                        impactDirection,
                        profile.SplitProjectileCount,
                        index,
                        profile.SplitAngleDegrees);
                if (direction.sqrMagnitude <= 0.0001f)
                {
                    direction = ProjectileLaunchPattern.GetDirection(
                        impactDirection,
                        profile.SplitProjectileCount,
                        index,
                        profile.SplitAngleDegrees);
                }

                Vector3 childOrigin = ProjectileLaunchPattern.GetOriginPosition(
                    hitPosition,
                    impactDirection,
                    profile.SplitProjectileCount,
                    index,
                    minimumSpacing);
                Vector3 childDirection = target != null
                    ? target.Position - childOrigin
                    : direction;
                if (childDirection.sqrMagnitude <= 0.0001f)
                {
                    childDirection = direction;
                }

                ProjectileData childData = projectile.Data;
                childData.OriginPosition = childOrigin;
                childData.RangeOriginPosition = hitPosition;
                childData.HasWorldRangeOrigin = true;
                childData.ProjectileSpawnOffset = 0f;
                childData.LaunchDirection = childDirection.normalized;
                childData.TrackingTarget = target;
                childData.PenetrationCount = projectile.RemainingPenetrationCount;
                childData.IsSplitChild = true;
                _queueSplitProjectile?.Invoke(childData, projectile, target);
            }
        }

        private static IDamageable FindNearestUnhitTarget(
            List<IDamageable> targets,
            Vector3 center,
            float radius,
            HashSet<int> excludedTargetIds,
            HashSet<int> selectedTargetIds)
        {
            if (targets == null || radius <= 0f)
            {
                return null;
            }

            float nearestDistanceSquared = radius * radius;
            IDamageable nearestTarget = null;
            for (int index = 0; index < targets.Count; index++)
            {
                IDamageable candidate = targets[index];
                if (candidate == null || !candidate.IsAlive || candidate.Transform == null)
                {
                    continue;
                }

                int candidateId = candidate.Transform.GetInstanceID();
                if (candidateId == 0 || excludedTargetIds.Contains(candidateId) ||
                    (selectedTargetIds != null && selectedTargetIds.Contains(candidateId)))
                {
                    continue;
                }

                float distanceSquared = (candidate.Position - center).sqrMagnitude;
                if (distanceSquared >= nearestDistanceSquared)
                {
                    continue;
                }

                nearestDistanceSquared = distanceSquared;
                nearestTarget = candidate;
            }

            if (nearestTarget != null && selectedTargetIds != null)
            {
                selectedTargetIds.Add(nearestTarget.Transform.GetInstanceID());
            }

            return nearestTarget;
        }

        /// <summary>构建携带攻击返能数据的通用伤害请求并分发命中结果。</summary>
        /// <param name="projectileData">当前攻击的数据快照。</param>
        /// <param name="profile">提供命中行为和返能比例的配置。</param>
        /// <param name="target">本次命中的伤害目标。</param>
        /// <param name="hitPosition">命中点的世界坐标。</param>
        /// <param name="hitNormal">命中表面的世界法线。</param>
        /// <param name="impactDirection">造成命中的世界方向。</param>
        /// <param name="projectile">普通投射物状态；Hitscan 不持有该状态。</param>
        private void SubmitHit(
            ProjectileData projectileData,
            ProjectileProfileSO profile,
            IDamageable target,
            Vector3 hitPosition,
            Vector3 hitNormal,
            Vector3 impactDirection,
            ProjectileState projectile)
        {
            projectileData = ApplyDamageModifiers(projectileData, profile, target);
            DamageRequest request = new()
            {
                AttackInstanceId = projectileData.AttackInstanceId,
                EnergyCost = Mathf.Max(0f, projectileData.EnergyCost),
                PaidEnergyCostSnapshot = projectileData.PaidEnergyCostSnapshot,
                HasPaidEnergyCostSnapshot = projectileData.HasPaidEnergyCostSnapshot,
                KillEnergyRefundRatio = profile != null
                    ? profile.KillEnergyRefundRatio
                    : 0f,
                SourceInstanceId = projectileData.SourceInstanceId,
                DamageAmount = projectileData.DamageAmount,
                DamageType = projectileData.DamageType,
                HitPosition = hitPosition,
                HitNormal = hitNormal,
                ImpactDirection = impactDirection,
                WasCritical = projectileData.WasCritical
            };
            _effectDispatcher?.DispatchBeforeHitEffects(profile, request, target);
            DamageResult result = default;
            _damageProcessor?.TryApplyDamage(target, in request, out result);
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
                _effectDispatcher?.DispatchHitEffects(profile, request, result, target);
            }

            _publishHitProcessed?.Invoke(new ProjectileHitContext(
                projectileData,
                target,
                hitPosition,
                hitNormal,
                result));
        }

        /// <summary>按目标生命比例和连续命中层数调整本次伤害。</summary>
        private ProjectileData ApplyDamageModifiers(
            ProjectileData projectileData,
            ProjectileProfileSO profile,
            IDamageable target)
        {
            if (profile == null || target?.Transform == null)
            {
                return projectileData;
            }

            Enemy enemy = target.Transform.GetComponentInParent<Enemy>();
            if (enemy == null)
            {
                return projectileData;
            }

            float multiplier = 1f;
            if (profile.ExecutionHealthThreshold > 0f && enemy.MaxHealth > 0f &&
                enemy.CurrentHealth / enemy.MaxHealth <= profile.ExecutionHealthThreshold)
            {
                multiplier *= Mathf.Max(1f, profile.ExecutionDamageMultiplier);
            }

            if (profile.ComboDamageBonusPerHit > 0f && profile.ComboMaximumStacks > 0 &&
                profile.ComboResetSeconds > 0f)
            {
                int targetId = enemy.GetInstanceID();
                long key = ((long)projectileData.SourceInstanceId << 32) ^ (uint)targetId;
                float currentTime = Time.time;
                int hitCount = 1;
                if (_comboHits.TryGetValue(key, out ComboHitState state) &&
                    currentTime - state.LastHitTime <= profile.ComboResetSeconds)
                {
                    hitCount = state.HitCount + 1;
                }

                int comboStacks = Mathf.Clamp(
                    hitCount - 1,
                    0,
                    profile.ComboMaximumStacks);
                multiplier *= 1f + comboStacks * profile.ComboDamageBonusPerHit;
                _comboHits[key] = new ComboHitState(
                    hitCount,
                    currentTime,
                    profile.ComboResetSeconds);
                RemoveExpiredComboHits(currentTime);
            }

            projectileData.DamageAmount *= multiplier;
            return projectileData;
        }

        /// <summary>低频清理已超过重置时间的连续命中计数。</summary>
        private void RemoveExpiredComboHits(float currentTime)
        {
            if (currentTime < _nextComboCleanupTime)
            {
                return;
            }

            _nextComboCleanupTime = currentTime + 5f;
            _expiredComboKeys.Clear();
            foreach (KeyValuePair<long, ComboHitState> entry in _comboHits)
            {
                if (currentTime - entry.Value.LastHitTime > entry.Value.ResetSeconds)
                {
                    _expiredComboKeys.Add(entry.Key);
                }
            }

            for (int index = 0; index < _expiredComboKeys.Count; index++)
            {
                _comboHits.Remove(_expiredComboKeys[index]);
            }
        }

        private readonly struct ComboHitState
        {
            public readonly int HitCount;
            public readonly float LastHitTime;
            public readonly float ResetSeconds;

            public ComboHitState(int hitCount, float lastHitTime, float resetSeconds)
            {
                HitCount = hitCount;
                LastHitTime = lastHitTime;
                ResetSeconds = resetSeconds;
            }
        }

        private static int ResolveTargetLayer(ProjectileTargetMode targetMode)
        {
            return targetMode == ProjectileTargetMode.Player
                ? SpatialLayers.Player
                : SpatialLayers.Enemy;
        }
    }
}
