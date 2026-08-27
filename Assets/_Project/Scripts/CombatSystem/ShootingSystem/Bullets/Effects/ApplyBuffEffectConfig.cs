using Interfaces;
using Services;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>把配置的 Buff 施加给命中的 Buffable 目标。</summary>
    [CreateAssetMenu(menuName = "ShootingSystem/Effects/Apply Buff")]
    public class ApplyBuffEffectConfig : BulletEffectConfig
    {
        public BuffConfig Buff;

        public override void Execute(BulletEffectContext context, IPooledEffectService effectService)
        {
            if (Buff == null) return;
            if (context.IsKill) return;
            var targetObj = context.TargetObject;
            if (targetObj == null) return;
            var buffable = targetObj.GetComponentInParent<IBuffable>();
            buffable?.ApplyBuff(Buff, context.SourceId);
        }
    }
}
