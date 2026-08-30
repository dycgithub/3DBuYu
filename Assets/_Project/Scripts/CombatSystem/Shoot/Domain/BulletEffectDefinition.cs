using Services;
using UnityEngine;
using EffectSystem;

namespace CombatSystem
{
    public abstract class BulletEffectDefinition : ScriptableObject
    {
        public abstract void Execute(BulletEffectContext context, IEffectService effectService);
    }

    [CreateAssetMenu(menuName = "Combat/Shoot Effects/Apply Buff")]
    public sealed class ApplyBuffEffectDefinition : BulletEffectDefinition
    {
        public BuffDefinition Buff;

        public override void Execute(BulletEffectContext context, IEffectService effectService)
        {
            if (Buff == null || context.IsKill || context.TargetObject == null)
                return;

            context.TargetObject.GetComponentInParent<IBuffable>()?.ApplyBuff(Buff, context.SourceId);
        }
    }

    [CreateAssetMenu(menuName = "Combat/Shoot Effects/Play VFX")]
    public sealed class PlayVfxEffectDefinition : BulletEffectDefinition
    {
        public EffectId Effect = EffectId.None;
        public bool AttachToTarget;

        public override void Execute(BulletEffectContext context, IEffectService effectService)
        {
            if (Effect == EffectId.None || effectService == null)
                return;

            Transform parent = null;
            Vector3 position = context.HitPoint;
            if (AttachToTarget && context.TargetObject != null)
            {
                parent = context.TargetObject.transform;
                position = parent.position;
            }

            effectService.Play(Effect, position, parent);
        }
    }
}
