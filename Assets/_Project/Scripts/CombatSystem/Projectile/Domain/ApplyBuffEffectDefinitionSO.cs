using EffectSystem;
using Services;
using UnityEngine;
using UnityEngine.Serialization;

namespace CombatSystem
{
    /// <summary>在有效投射物命中后向目标施加一个 Buff。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Effects/Apply Buff")]
    public sealed class ApplyBuffEffectDefinitionSO : ProjectileEffectDefinitionSO
    {
        /// <summary>要施加给命中目标的 Buff 定义。</summary>
        [FormerlySerializedAs("Buff")]
        public BuffDefinitionSO BuffDefinition;

        /// <summary>将配置 Buff 交给命中目标的接收接口。</summary>
        public override void ExecuteEffect(ProjectileEffectContext context, IEffectService effectService)
        {
            if (BuffDefinition == null || context.WasKilled || context.TargetGameObject == null)
            {
                return;
            }

            context.TargetGameObject.GetComponentInParent<IBuffReceiver>()?.ApplyBuff(
                BuffDefinition,
                context.SourceInstanceId);
        }
    }
}
