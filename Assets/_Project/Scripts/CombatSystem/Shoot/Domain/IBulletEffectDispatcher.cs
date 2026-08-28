using Interfaces;
using UnityEngine;

namespace CombatSystem
{
    public interface IBulletEffectDispatcher
    {
        void DispatchHit(BulletProfile profile, DamageRequest request, DamageResult result, IDamageable target);
        void DispatchTrigger(BulletProfile profile, DamageRequest request, IDamageable target);
        void DispatchExpired(BulletProfile profile, Vector3 position);
    }
}
