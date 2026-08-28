using Interfaces;
using Services;
using UnityEngine;

namespace CombatSystem
{
    public sealed class BulletEffectDispatcher : IBulletEffectDispatcher
    {
        private readonly IPooledEffectService _effectService;

        public BulletEffectDispatcher(IPooledEffectService effectService)
        {
            _effectService = effectService;
        }

        public void DispatchHit(BulletProfile profile, DamageRequest request, DamageResult result, IDamageable target)
        {
            if (profile?.OnHitEffects == null || !result.WasApplied)
                return;
            ExecuteAll(profile.OnHitEffects, CreateContext(profile, request, result, target));
        }

        public void DispatchTrigger(BulletProfile profile, DamageRequest request, IDamageable target)
        {
            if (profile?.OnTriggerEffects == null)
                return;
            BulletEffectContext context = CreateContext(profile, request, default, target);
            ExecuteAll(profile.OnTriggerEffects, context);
        }

        public void DispatchExpired(BulletProfile profile, Vector3 position)
        {
            if (profile?.OnExpiredEffects == null)
                return;
            ExecuteAll(profile.OnExpiredEffects, new BulletEffectContext
            {
                HitPoint = position,
                Profile = profile
            });
        }

        private void ExecuteAll(System.Collections.Generic.List<BulletEffectDefinition> effects, BulletEffectContext context)
        {
            for (int i = 0; i < effects.Count; i++)
                effects[i]?.Execute(context, _effectService);
        }

        private static BulletEffectContext CreateContext(
            BulletProfile profile,
            DamageRequest request,
            DamageResult result,
            IDamageable target)
        {
            GameObject targetObject = target?.Transform != null ? target.Transform.gameObject : null;
            return new BulletEffectContext
            {
                SourceId = request.SourceId,
                TargetInstanceId = targetObject != null ? targetObject.GetInstanceID() : 0,
                TargetObject = targetObject,
                IsKill = result.IsKill,
                HitPoint = request.HitPoint,
                Damage = result.ActualDamage,
                Profile = profile
            };
        }
    }
}
