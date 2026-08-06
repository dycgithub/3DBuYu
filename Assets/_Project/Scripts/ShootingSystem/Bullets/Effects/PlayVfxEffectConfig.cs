using UnityEngine;

namespace ShootingSystem.Bullets.Effects
{
    [CreateAssetMenu(menuName = "ShootingSystem/Effects/Play VFX")]
    public class PlayVfxEffectConfig : BulletEffectConfig
    {
        public GameObject VfxPrefab;
        public bool AttachToTarget;

        public override void Execute(BulletEffectContext context)
        {
            if (VfxPrefab == null) return;

            if (AttachToTarget)
            {
                var targetObj = Resources.EntityIdToObject(context.TargetInstanceId) as GameObject;
                if (targetObj != null)
                    Object.Instantiate(VfxPrefab, targetObj.transform.position, Quaternion.identity);
            }
            else
            {
                Object.Instantiate(VfxPrefab, (Vector3)context.HitPoint, Quaternion.identity);
            }
        }
    }
}
