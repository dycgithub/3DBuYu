using Interfaces;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 只负责把物理查询结果转换成最近的可伤害目标，不负责扣血和回收子弹。
    /// </summary>
    public sealed class ProjectileHitQuery : IProjectileHitQuery
    {
        private readonly RaycastHit[] _sweepBuffer = new RaycastHit[64];
        private readonly RaycastHit[] _raycastBuffer = new RaycastHit[64];
        private readonly int _enemyLayerMask;

        public ProjectileHitQuery()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            _enemyLayerMask = enemyLayer >= 0
                ? 1 << enemyLayer
                : Physics.DefaultRaycastLayers;
        }

        public bool TrySweep(
            ProjectileRuntime projectile,
            float distance,
            out RaycastHit hit,
            out IDamageable target)
        {
            int hitCount = Physics.SphereCastNonAlloc(
                projectile.Position,
                Mathf.Max(0f, projectile.Info.Radius),
                projectile.Direction,
                _sweepBuffer,
                distance,
                _enemyLayerMask,
                QueryTriggerInteraction.Collide);

            hit = default;
            target = null;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidateHit = _sweepBuffer[i];
                Collider collider = candidateHit.collider;
                if (collider == null)
                    continue;

                IDamageable candidate = collider.GetComponentInParent<IDamageable>();
                if (candidate == null || !candidate.IsAlive)
                    continue;

                int targetId = candidate.Transform != null
                    ? candidate.Transform.GetInstanceID()
                    : 0;
                if (targetId != 0 && projectile.HitTargetIds.Contains(targetId))
                    continue;

                if (candidateHit.distance >= nearestDistance)
                    continue;

                nearestDistance = candidateHit.distance;
                hit = candidateHit;
                target = candidate;
            }

            return target != null;
        }

        public bool TryRaycast(
            ProjectileInfo info,
            out RaycastHit hit,
            out IDamageable target)
        {
            int hitCount = Physics.RaycastNonAlloc(
                info.Origin,
                info.Direction,
                _raycastBuffer,
                Mathf.Max(0f, info.MaxDistance),
                _enemyLayerMask,
                QueryTriggerInteraction.Collide);

            hit = default;
            target = null;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit candidateHit = _raycastBuffer[i];
                if (candidateHit.collider == null)
                    continue;

                IDamageable candidate = candidateHit.collider.GetComponentInParent<IDamageable>();
                if (candidate == null || !candidate.IsAlive || candidateHit.distance >= nearestDistance)
                    continue;

                nearestDistance = candidateHit.distance;
                hit = candidateHit;
                target = candidate;
            }

            return target != null;
        }
    }
}
