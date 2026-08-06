using UnityEngine;
using Interfaces;
using ShootingSystem.Buffs;

namespace ShootingSystem.Bullets.Effects
{
    [CreateAssetMenu(menuName = "ShootingSystem/Effects/Apply Buff")]
    public class ApplyBuffEffectConfig : BulletEffectConfig
    {
        public Buffs.BuffConfig Buff;

        public override void Execute(BulletEffectContext context)
        {
            if (Buff == null) return;
            var targetObj = Resources.EntityIdToObject(context.TargetInstanceId) as GameObject;
            if (targetObj == null) return;
            var buffable = targetObj.GetComponentInParent<IBuffable>();
            buffable?.ApplyBuff(Buff);
        }
    }
}
