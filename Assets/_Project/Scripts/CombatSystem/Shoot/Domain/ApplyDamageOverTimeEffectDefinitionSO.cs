using EffectSystem;
using Services;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>按实际命中伤害比例给目标施加可叠加的灼烧效果。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Effects/Apply Damage Over Time")]
    public sealed class ApplyDamageOverTimeEffectDefinitionSO : ProjectileEffectDefinitionSO
    {
        /// <summary>每层每秒伤害占命中伤害的比例。</summary>
        [Range(0f, 1f)] public float DamageRatio = 0.15f;
        /// <summary>每层灼烧持续时间。</summary>
        [Min(0f)] public float DurationSeconds = 3f;

        /// <summary>将持续伤害计时交给场景 Buff 服务。</summary>
        public override void ExecuteEffect(ProjectileEffectContext context, IEffectService effectService)
        {
            if (context.BuffService == null || context.TargetGameObject == null ||
                context.AppliedDamage <= 0f)
            {
                return;
            }

            IBuffTarget target = context.TargetGameObject.GetComponentInParent<IBuffTarget>();
            if (target == null)
            {
                return;
            }

            context.BuffService.ApplyDamageOverTime(
                target,
                context.AppliedDamage * Mathf.Clamp01(DamageRatio),
                Mathf.Max(0f, DurationSeconds),
                context.SourceInstanceId);
        }
    }
}
