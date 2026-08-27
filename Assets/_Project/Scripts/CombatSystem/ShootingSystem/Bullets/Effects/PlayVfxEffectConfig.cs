using Services;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>在命中位置或目标对象上创建视觉效果。</summary>
    [CreateAssetMenu(menuName = "ShootingSystem/Effects/Play VFX")]
    public class PlayVfxEffectConfig : BulletEffectConfig
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

            effectService.Play(
                VfxPrefab,
                position,
                Quaternion.identity,
                Vector3.one,
                Lifetime,
                parent);
        }
    }
}
