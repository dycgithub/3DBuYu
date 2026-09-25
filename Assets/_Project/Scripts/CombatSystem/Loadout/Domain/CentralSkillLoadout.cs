using System.Collections.Generic;

namespace CombatSystem
{
    /// <summary>
    /// 从中央背包提取来源身份、强度和有序技能，为每个物品实例创建或恢复独立卡牌状态。
    /// </summary>
    public sealed class CentralSkillLoadout
    {
        private readonly ItemCombatDefinitionResolver _definitionResolver;
        private readonly SkillHand _hand;
        private float _energyCostMultiplier = 1f;

        internal float EnergyCostMultiplier => _energyCostMultiplier;

        public SkillHand Hand => _hand;

        public CentralSkillLoadout(ItemCombatDefinitionResolver definitionResolver, SkillHand hand)
        {
            _definitionResolver = definitionResolver;
            _hand = hand;
        }

        public void BeginRun()
        {
            _hand.Clear();
            _energyCostMultiplier = 1f;
        }

        /// <summary>按来源物品实例同步技能和强度，保留仍存在技能的次数和冷却。</summary>
        public void ReplaceItems(IEnumerable<ItemVM> items)
        {
            var nextSkills = new List<SkillCardState>();
            _energyCostMultiplier = 1f;
            if (items != null)
            {
                foreach (ItemVM item in items)
                {
                    if (item == null)
                    {
                        continue;
                    }

                    if (_definitionResolver.TryResolveCombatScope(
                            item.Definition,
                            out CombatScope scope) &&
                        (scope == CombatScope.Central || scope == CombatScope.CentralAndTransmitter))
                    {
                        ItemCombatDefinitionSO combat = item.Definition.CombatDefinition;
                        float itemMultiplier = combat != null
                            ? combat.CentralEnergyCostMultiplier
                            : 1f;
                        float powerScale = Supply.ItemPowerTable.GetScale(item.PowerLevel);
                        _energyCostMultiplier *= 1f + (itemMultiplier - 1f) * powerScale;
                    }

                    IReadOnlyList<SkillDefinitionSO> definitions =
                        _definitionResolver.ResolveCentralSkillDefinitions(item.Definition);
                    if (definitions == null || definitions.Count == 0)
                    {
                        continue;
                    }

                    SkillGrant grant = _definitionResolver.ResolveCentralSkillGrant(item.Definition);

                    SkillCardState existing = _hand.FindBySourceItemInstanceId(item.InstanceId);
                    if (existing != null && existing.Matches(definitions, grant, item.PowerLevel))
                    {
                        nextSkills.Add(existing);
                        continue;
                    }

                    SkillCardState created = SkillCardState.Create(
                        item.InstanceId,
                        item.Definition,
                        grant,
                        definitions,
                        item.PowerLevel);
                    if (created != null)
                    {
                        nextSkills.Add(created);
                    }
                }
            }

            _hand.Rebuild(nextSkills);
        }

        public void Clear()
        {
            _hand.Clear();
            _energyCostMultiplier = 1f;
        }
    }
}
