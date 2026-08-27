using Interfaces;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>投射物模拟所需的效果派发端口。</summary>
    public interface IBulletEffectDispatcher
    {
        void DispatchHit(
            BulletProfile profile,
            DamageRequest request,
            DamageResult result,
            IDamageable target);

        void DispatchTrigger(
            BulletProfile profile,
            DamageRequest request,
            IDamageable target);

        void DispatchExpired(BulletProfile profile, Vector3 position);
    }
}
