using Services;
using UnityEngine;

namespace CombatSystem
{
    public abstract class BulletEffectDefinition : ScriptableObject
    {
        public abstract void Execute(BulletEffectContext context, IPooledEffectService effectService);
    }

    [CreateAssetMenu(menuName = "Combat/Shoot Effects/Apply Buff")]
    public sealed class ApplyBuffEffectDefinition : BulletEffectDefinition
    {
        public BuffDefinition Buff;

        public override void Execute(BulletEffectContext context, IPooledEffectService effectService)
        {
            if (Buff == null || context.IsKill || context.TargetObject == null)
                return;

            context.TargetObject.GetComponentInParent<IBuffable>()?.ApplyBuff(Buff, context.SourceId);
        }
    }

    [CreateAssetMenu(menuName = "Combat/Shoot Effects/Play VFX")]
    public sealed class PlayVfxEffectDefinition : BulletEffectDefinition
    {
        public GameObject VfxPrefab;
        public bool AttachToTarget;
        [Min(0.01f)] public float Lifetime = 2f;

        public override void Execute(BulletEffectContext context, IPooledEffectService effectService)
        {
            if (VfxPrefab == null || effectService == null)
                return;

            Transform parent = null;
            Vector3 position = context.HitPoint;
            if (AttachToTarget && context.TargetObject != null)
            {
                parent = context.TargetObject.transform;
                position = parent.position;
            }

            effectService.Play(VfxPrefab, position, Quaternion.identity, Vector3.one, Lifetime, parent);
        }
    }
}
