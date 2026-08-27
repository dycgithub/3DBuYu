using Interfaces;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>投射物模拟所需的命中查询端口。</summary>
    public interface IProjectileHitQuery
    {
        bool TrySweep(
            ProjectileRuntime projectile,
            float distance,
            out RaycastHit hit,
            out IDamageable target);

        bool TryRaycast(
            ProjectileInfo info,
            out RaycastHit hit,
            out IDamageable target);
    }
}
