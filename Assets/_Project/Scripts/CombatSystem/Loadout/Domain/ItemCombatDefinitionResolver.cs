using System;
using System.Collections.Generic;

namespace CombatSystem
{
    /// <summary>
    /// 解析物品战斗作用范围和共享消费配置；技能列表只读取物品的有序数组。
    /// </summary>
    public sealed class ItemCombatDefinitionResolver
    {
        /// <summary>获取物品的显式战斗配置；未配置时返回空引用。</summary>
        public ItemCombatDefinitionSO ResolveCombatDefinition(ItemDefinition definition)
        {
            return definition?.CombatDefinition;
        }

        /// <summary>
        /// 解析显式战斗配置或最终物品 ID 的作用域。
        /// 未配置且不在最终 ID 表中的物品返回 None。
        /// </summary>
        public CombatScope ResolveCombatScope(ItemDefinition definition)
        {
            if (definition?.CombatDefinition != null)
            {
                return definition.CombatDefinition.CombatScope;
            }

            return ItemCombatScopePolicy.TryResolve(definition?.Id, out CombatScope scope)
                ? scope
                : CombatScope.None;
        }

        /// <summary>尝试解析物品的有效战斗作用域。</summary>
        public bool TryResolveCombatScope(ItemDefinition definition, out CombatScope scope)
        {
            scope = ResolveCombatScope(definition);
            return scope != CombatScope.None;
        }

        public SkillGrant ResolveCentralSkillGrant(ItemDefinition definition)
        {
            if (!TryResolveCombatScope(definition, out CombatScope scope)
                || (scope != CombatScope.Central && scope != CombatScope.CentralAndTransmitter))
            {
                return null;
            }

            return definition?.CombatDefinition?.CentralSkillGrant;
        }

        /// <summary>返回物品有序技能配置；未作用于中心或未配置数组时返回空集合。</summary>
        public IReadOnlyList<SkillDefinitionSO> ResolveCentralSkillDefinitions(ItemDefinition definition)
        {
            if (!TryResolveCombatScope(definition, out CombatScope scope)
                || (scope != CombatScope.Central && scope != CombatScope.CentralAndTransmitter))
            {
                return Array.Empty<SkillDefinitionSO>();
            }

            return definition?.SkillDefinitions ?? Array.Empty<SkillDefinitionSO>();
        }
    }
}
