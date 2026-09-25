using CombatSystem;

namespace Supply
{
    /// <summary>将现有 ItemCatalog 转换为抽奖使用的 15 件物品池。</summary>
    public static class SupplyItemPoolFactory
    {
        /// <summary>从现有物品目录创建等权物品池。</summary>
        /// <param name="catalog">当前项目物品目录。</param>
        /// <returns>目录为空时返回空池。</returns>
        public static SupplyItemPool FromCatalog(ItemCatalog catalog)
        {
            if (catalog == null)
            {
                return SupplyItemPool.Create();
            }

            var entries = new SupplyItemEntry[catalog.Items.Count];
            int count = 0;
            for (int index = 0; index < catalog.Items.Count; index++)
            {
                ItemDefinition definition = catalog.Items[index];
                if (definition == null || string.IsNullOrEmpty(definition.Id))
                {
                    continue;
                }

                entries[count++] = new SupplyItemEntry(
                    definition.Id,
                    1,
                    ResolveRole(definition));
            }

            if (count == entries.Length)
            {
                return SupplyItemPool.Create(entries);
            }

            var validEntries = new SupplyItemEntry[count];
            System.Array.Copy(entries, validEntries, count);
            return SupplyItemPool.Create(validEntries);
        }

        /// <summary>按现有战斗能力推导抽奖职责，不改变装备作用域。</summary>
        /// <param name="definition">物品静态定义。</param>
        /// <returns>物品可参与的职责集合。</returns>
        private static SupplyItemRole ResolveRole(ItemDefinition definition)
        {
            SupplyItemRole role = SupplyItemRole.None;
            if (definition.SkillDefinitions != null && definition.SkillDefinitions.Length > 0)
            {
                role |= SupplyItemRole.Skill;
            }

            ItemCombatDefinitionSO combat = definition.CombatDefinition;
            if (combat == null)
            {
                return role == SupplyItemRole.None ? SupplyItemRole.Firepower : role;
            }

            if (combat.TransmitterProjectileProfile != null)
            {
                role |= SupplyItemRole.Projectile;
            }

            TransmitterAttackModifierDefinitionSO[] modifiers =
                combat.TransmitterAttackModifierDefinitions;
            if (modifiers != null)
            {
                for (int index = 0; index < modifiers.Length; index++)
                {
                    role |= ResolveModifierRole(modifiers[index]);
                }
            }

            if (combat.TransmitterAdditiveDamageBonus > 0f)
            {
                role |= SupplyItemRole.Firepower;
            }

            if (role == SupplyItemRole.None && combat.AppliesToTransmitterLoadout)
            {
                role |= SupplyItemRole.Firepower;
            }

            return role;
        }

        /// <summary>将发射器修正类型映射到抽奖职责。</summary>
        /// <param name="modifier">发射器修正定义。</param>
        /// <returns>对应职责；未知类型不增加职责。</returns>
        private static SupplyItemRole ResolveModifierRole(
            TransmitterAttackModifierDefinitionSO modifier)
        {
            switch (modifier)
            {
                case AddDamageAttackModifierDefinitionSO:
                case MultiplyDamageAttackModifierDefinitionSO:
                case MultiplyAttackSpeedAttackModifierDefinitionSO:
                    return SupplyItemRole.Firepower;
                case AddProjectileCountAttackModifierDefinitionSO:
                case AddPenetrationAttackModifierDefinitionSO:
                case MultiplyCollisionRadiusAttackModifierDefinitionSO:
                case SetDamageTypeAttackModifierDefinitionSO:
                    return SupplyItemRole.Projectile;
                default:
                    return SupplyItemRole.None;
            }
        }
    }
}
