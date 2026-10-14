using CombatSystem;
using UnityEngine;

namespace EnemySystem.AreaEffect.Implement
{
    /// <summary>为范围减速复制一份可设置倍率的运行时 Buff 定义。</summary>
    internal static class EnemyAreaEffectBuffFactory
    {
        /// <summary>复用或创建减速 Buff 副本，并更新本敌人的减速倍率。</summary>
        /// <param name="attributes">范围精英静态配置。</param>
        /// <param name="runtimeBuff">本组件上一次创建的运行时副本。</param>
        /// <param name="ownerName">副本所属敌人的名称。</param>
        /// <returns>可供现有 Buff 服务使用的定义副本。</returns>
        internal static BuffDefinitionSO GetSlowBuff(
            EnemyAttributesSO attributes,
            BuffDefinitionSO runtimeBuff,
            string ownerName)
        {
            if (attributes == null || attributes.areaSlowBuff == null)
            {
                return null;
            }

            if (runtimeBuff == null)
            {
                BuffDefinitionSO source = attributes.areaSlowBuff;
                runtimeBuff = ScriptableObject.CreateInstance<BuffDefinitionSO>();
                runtimeBuff.name = $"{ownerName}_SlowBuff";
                runtimeBuff.ModifierType = source.ModifierType;
                runtimeBuff.StackGroupId = source.StackGroupId;
                runtimeBuff.Polarity = source.Polarity;
                runtimeBuff.DurationSeconds = source.DurationSeconds;
                runtimeBuff.HealingPerSecond = source.HealingPerSecond;
                runtimeBuff.StackPolicy = source.StackPolicy;
                runtimeBuff.MaximumStackCount = source.MaximumStackCount;
                runtimeBuff.VisualEffectPrefab = source.VisualEffectPrefab;
            }

            runtimeBuff.ModifierValue = Mathf.Clamp(attributes.areaSlowMultiplier, 0f, 1f);
            return runtimeBuff;
        }
    }
}
