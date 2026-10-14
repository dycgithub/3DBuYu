using EffectSystem;
using Interfaces;
using Services;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>将有效投射物命中的冲击沿弹道方向提交给敌人运动边界。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Effects/Knockback")]
    public sealed class KnockbackEffectDefinitionSO : ProjectileEffectDefinitionSO
    {
        /// <summary>默认击退冲量。</summary>
        [Min(0f)] public float Impulse = 5f;

        /// <summary>通过敌人运动接口施加一次命中击退。</summary>
        public override void ExecuteEffect(ProjectileEffectContext context, IEffectService effectService)
        {
            if (context.TargetGameObject == null)
            {
                return;
            }

            IEnemyMotionEffectTarget motionTarget = context.TargetGameObject
                .GetComponentInParent<IEnemyMotionEffectTarget>();
            if (motionTarget == null)
            {
                return;
            }

            Vector3 direction = context.DamageRequest.ImpactDirection;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = -context.DamageRequest.HitNormal;
            }

            if (direction.sqrMagnitude > 0.0001f)
            {
                motionTarget.AddExternalImpulse(direction.normalized * Mathf.Max(0f, Impulse));
            }
        }
    }
}
