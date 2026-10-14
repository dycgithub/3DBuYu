using Interfaces;
using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 使用物理查询寻找投射物路径上最近的有效伤害目标。
    /// </summary>
    public sealed class ProjectileCollisionQuery :
        IProjectileCollisionQuery,
        IProjectileCollisionRangeQuery
    {
        private const int InitialHitBufferCapacity = 64;

        private RaycastHit[] _sweepBuffer = new RaycastHit[InitialHitBufferCapacity];
        private RaycastHit[] _raycastBuffer = new RaycastHit[InitialHitBufferCapacity];
        private readonly int _enemyLayerMask;
        private readonly int _defaultLayerMask;

        /// <summary>创建使用 Enemy 层作为默认查询范围的碰撞查询。</summary>
        public ProjectileCollisionQuery()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            _enemyLayerMask = enemyLayer >= 0 ? 1 << enemyLayer : Physics.DefaultRaycastLayers;
            _defaultLayerMask = Physics.DefaultRaycastLayers;
        }

        /// <summary>沿投射物下一步路径执行球形扫掠并返回最近目标。</summary>
        /// <param name="projectile">待查询的投射物状态。</param>
        /// <param name="travelDistance">本次模拟的移动距离。</param>
        /// <param name="hit">命中时输出最近物理命中。</param>
        /// <param name="target">命中时输出有效伤害目标。</param>
        /// <returns>找到有效目标时返回 <c>true</c>。</returns>
        public bool TryFindSweepCollision(
            ProjectileState projectile,
            float travelDistance,
            out RaycastHit hit,
            out IDamageable target)
        {
            int hitCount;
            while (true)
            {
                hitCount = Physics.SphereCastNonAlloc(
                    projectile.Position,
                    Mathf.Max(0f, projectile.Data.CollisionRadius),
                    projectile.Direction,
                    _sweepBuffer,
                    travelDistance,
                    ResolveLayerMask(projectile.Data.TargetMode),
                    QueryTriggerInteraction.Collide);
                if (hitCount < _sweepBuffer.Length)
                {
                    break;
                }

                System.Array.Resize(ref _sweepBuffer, _sweepBuffer.Length * 2);
            }

            hit = default;
            target = null;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidateHit = _sweepBuffer[i];
                Collider collider = candidateHit.collider;
                if (collider == null)
                {
                    continue;
                }

                IDamageable candidate = collider.GetComponentInParent<IDamageable>();
                if (!candidate.IsAliveAndValid() ||
                    !MatchesTargetMode(candidate, projectile.Data.TargetMode))
                {
                    continue;
                }

                int targetInstanceId = candidate.Transform != null
                    ? candidate.Transform.GetInstanceID()
                    : 0;
                if (targetInstanceId != 0
                    && projectile.HitTargetInstanceIds.Contains(targetInstanceId))
                {
                    continue;
                }

                if (candidateHit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = candidateHit.distance;
                hit = candidateHit;
                target = candidate;
            }
            return target != null;
        }

        /// <summary>收集球形扫掠中的唯一有效目标，并按命中距离排序。</summary>
        public int QuerySweepCollisions(
            ProjectileState projectile,
            float travelDistance,
            List<ProjectileCollision> collisions)
        {
            if (projectile == null || collisions == null)
            {
                return 0;
            }

            collisions.Clear();
            int hitCount = CollectSweepHits(projectile, travelDistance);
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidateHit = _sweepBuffer[i];
                Collider collider = candidateHit.collider;
                if (collider == null)
                {
                    continue;
                }

                IDamageable candidate = collider.GetComponentInParent<IDamageable>();
                if (!IsValidTarget(candidate, projectile.Data.TargetMode) ||
                    IsAlreadyHit(projectile, candidate) ||
                    ContainsTarget(collisions, candidate))
                {
                    continue;
                }

                int insertIndex = collisions.Count;
                for (int index = 0; index < collisions.Count; index++)
                {
                    if (candidateHit.distance < collisions[index].Hit.distance)
                    {
                        insertIndex = index;
                        break;
                    }
                }

                collisions.Insert(insertIndex, new ProjectileCollision(candidateHit, candidate));
            }

            return collisions.Count;
        }

        /// <summary>沿 Hitscan 路径执行射线查询并返回最近目标。</summary>
        /// <param name="projectileData">包含起点、方向和最大距离的投射物数据。</param>
        /// <param name="hit">命中时输出最近物理命中。</param>
        /// <param name="target">命中时输出有效伤害目标。</param>
        /// <returns>找到有效目标时返回 <c>true</c>。</returns>
        public bool TryFindRaycastCollision(
            ProjectileData projectileData,
            out RaycastHit hit,
            out IDamageable target)
        {
            int hitCount;
            while (true)
            {
                hitCount = Physics.RaycastNonAlloc(
                    projectileData.OriginPosition,
                    projectileData.LaunchDirection,
                    _raycastBuffer,
                    Mathf.Max(0f, projectileData.MaximumTravelDistance),
                    ResolveLayerMask(projectileData.TargetMode),
                    QueryTriggerInteraction.Collide);
                if (hitCount < _raycastBuffer.Length)
                {
                    break;
                }

                System.Array.Resize(ref _raycastBuffer, _raycastBuffer.Length * 2);
            }

            hit = default;
            target = null;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidateHit = _raycastBuffer[i];
                if (candidateHit.collider == null)
                {
                    continue;
                }

                IDamageable candidate = candidateHit.collider.GetComponentInParent<IDamageable>();
                if (!candidate.IsAliveAndValid() ||
                    !MatchesTargetMode(candidate, projectileData.TargetMode) ||
                    candidateHit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = candidateHit.distance;
                hit = candidateHit;
                target = candidate;
            }
            return target != null;
        }

        /// <summary>按目标阵营选择物理查询层。</summary>
        private int ResolveLayerMask(ProjectileTargetMode targetMode)
        {
            return targetMode == ProjectileTargetMode.Player
                ? _defaultLayerMask
                : _enemyLayerMask;
        }

        /// <summary>执行一次物理扫掠并返回原始命中数量。</summary>
        private int CollectSweepHits(ProjectileState projectile, float travelDistance)
        {
            int hitCount;
            while (true)
            {
                hitCount = Physics.SphereCastNonAlloc(
                    projectile.Position,
                    Mathf.Max(0f, projectile.Data.CollisionRadius),
                    projectile.Direction,
                    _sweepBuffer,
                    travelDistance,
                    ResolveLayerMask(projectile.Data.TargetMode),
                    QueryTriggerInteraction.Collide);
                if (hitCount < _sweepBuffer.Length)
                {
                    return hitCount;
                }

                System.Array.Resize(ref _sweepBuffer, _sweepBuffer.Length * 2);
            }
        }

        /// <summary>确认目标有效且属于当前投射物阵营。</summary>
        private static bool IsValidTarget(IDamageable target, ProjectileTargetMode targetMode)
        {
            return target != null && target.IsAliveAndValid() && MatchesTargetMode(target, targetMode);
        }

        /// <summary>判断投射物是否已经命中过该目标。</summary>
        private static bool IsAlreadyHit(ProjectileState projectile, IDamageable target)
        {
            int targetInstanceId = target.Transform != null
                ? target.Transform.GetInstanceID()
                : 0;
            return targetInstanceId != 0 && projectile.HitTargetInstanceIds.Contains(targetInstanceId);
        }

        /// <summary>判断批量结果中是否已经包含同一个目标。</summary>
        private static bool ContainsTarget(List<ProjectileCollision> collisions, IDamageable target)
        {
            for (int index = 0; index < collisions.Count; index++)
            {
                if (ReferenceEquals(collisions[index].Target, target))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>确认候选目标属于投射物允许命中的阵营。</summary>
        private static bool MatchesTargetMode(
            IDamageable target,
            ProjectileTargetMode targetMode)
        {
            ICombatTarget combatTarget = target as ICombatTarget;
            if (combatTarget == null)
            {
                return false;
            }

            CombatTargetKind expectedKind = targetMode == ProjectileTargetMode.Player
                ? CombatTargetKind.Player
                : CombatTargetKind.Enemy;
            return combatTarget.TargetKind == expectedKind;
        }
    }
}
