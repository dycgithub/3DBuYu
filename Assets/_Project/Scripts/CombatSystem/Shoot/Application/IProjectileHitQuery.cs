using Interfaces;
using UnityEngine;

namespace CombatSystem
{
    public interface IProjectileHitQuery
    {
        bool TrySweep(ProjectileRuntime projectile, float distance, out RaycastHit hit, out IDamageable target);
        bool TryRaycast(ProjectileInfo info, out RaycastHit hit, out IDamageable target);
    }
}
