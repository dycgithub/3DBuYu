using Interfaces;
using Services;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 同步派发子弹结果效果。会在伤害结果确定后执行。
    /// </summary>
    public sealed class BulletEffectDispatcher : IBulletEffectDispatcher
    {
        private readonly IPooledEffectService _effectService;

        public BulletEffectDispatcher(IPooledEffectService effectService)
        {
            _effectService = effectService;
        }

        public void DispatchHit(
            BulletProfile profile,
            DamageRequest request,
            DamageResult result,
            IDamageable target)
        {
            if (profile == null || profile.OnHitEffects == null || !result.WasApplied)
                return;

            GameObject targetObject = target?.Transform != null
                ? target.Transform.gameObject
                : null;

            var context = new BulletEffectContext
            {
                SourceId = request.SourceId,
                TargetInstanceId = targetObject != null ? targetObject.GetInstanceID() : 0,
                TargetObject = targetObject,
                IsKill = result.IsKill,
                HitPoint = request.HitPoint,
                Damage = result.ActualDamage,
                Profile = profile
            };

            foreach (BulletEffectConfig effect in profile.OnHitEffects)
            {
                if (effect != null)
                    effect.Execute(context, _effectService);
            }
        }

        public void DispatchTrigger(
            BulletProfile profile,
            DamageRequest request,
            IDamageable target)
        {
            if (profile == null || profile.OnTriggerEffects == null)
                return;

            GameObject targetObject = target?.Transform != null
                ? target.Transform.gameObject
                : null;

            var context = new BulletEffectContext
            {
                SourceId = request.SourceId,
                TargetInstanceId = targetObject != null ? targetObject.GetInstanceID() : 0,
                TargetObject = targetObject,
                HitPoint = request.HitPoint,
                Profile = profile
            };

            foreach (BulletEffectConfig effect in profile.OnTriggerEffects)
            {
                if (effect != null)
                    effect.Execute(context, _effectService);
            }
        }

        public void DispatchExpired(BulletProfile profile, Vector3 position)
        {
            if (profile == null || profile.OnExpiredEffects == null)
                return;

            var context = new BulletEffectContext
            {
                HitPoint = position,
                Profile = profile
            };

            foreach (BulletEffectConfig effect in profile.OnExpiredEffects)
            {
                if (effect != null)
                    effect.Execute(context, _effectService);
            }
        }
    }
}
