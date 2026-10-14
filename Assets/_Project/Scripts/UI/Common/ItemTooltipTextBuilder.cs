using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CombatSystem;
using UnityEngine;

namespace _Project.UI.Common
{
    /// <summary>
    /// 把 ItemVM 和战斗配置转换为当前语言的 Tooltip 展示数据。
    /// 技能取自物品的有序数组，能量、次数和冷却按整卡共享配置显示一次。
    /// </summary>
    public static class ItemTooltipTextBuilder
    {
        /// <summary>
        /// 构建当前物品的 Tooltip 内容。
        /// </summary>
        /// <param name="item">要展示的网格物品。</param>
        /// <param name="gridType">物品当前所在网格类型。</param>
        /// <param name="price">由商店服务解析出的价格。</param>
        /// <returns>物品有定义时返回展示数据；否则返回 <c>null</c>。</returns>
        public static ItemTooltipContent Build(ItemVM item, GridType gridType, int price)
        {
            ItemDefinition definition = item?.Definition;
            if (definition == null)
            {
                return null;
            }

            var effects = new List<string>();
            string scope = string.Empty;
            ItemCombatDefinitionSO grant = definition.CombatDefinition;
            if (grant != null)
            {
                scope = FormatScope(grant.CombatScope);
                if (grant.AppliesToTransmitterLoadout)
                {
                    AddAttackModifiers(grant.TransmitterAttackModifierDefinitions, effects);
                }
                if (grant.AppliesToCentralLoadout)
                {
                    AddSkills(definition.SkillDefinitions, grant.CentralSkillGrant, effects);
                }
                if (grant.AppliesToTransmitterLoadout
                    && !Mathf.Approximately(grant.TransmitterAdditiveDamageBonus, 0f))
                {
                    effects.Add(L10n.Get("tooltip.transmitterDamage", grant.TransmitterAdditiveDamageBonus.ToString("0.##", CultureInfo.InvariantCulture)));
                }
            }

            string footprint = item.Width > 0 && item.Height > 0
                ? L10n.Get("tooltip.footprint", item.Width, item.Height, item.CoordinateSet.Count, GridUtilities.RotationHelper.GetRotationAngle(item.Direction))
                : L10n.Get("tooltip.noFootprint");

            bool hasPrice = gridType == GridType.Shop;
            string priceText = hasPrice
                ? price <= 0 ? L10n.Get("tooltip.free") : L10n.Get("tooltip.points", price)
                : string.Empty;

            return new ItemTooltipContent(
                definition.DisplayName,
                definition.Description,
                string.Join("\n", effects),
                scope,
                footprint,
                priceText,
                hasPrice,
                definition.Icon,
                definition.Color,
                item.CoordinateSet,
                item.RotationOffset,
                item.Width,
                item.Height);
        }

        /// <summary>将战斗配置作用范围转换为玩家可读文本。</summary>
        private static string FormatScope(CombatScope scope)
            => L10n.Get(scope == CombatScope.Central ? "tooltip.centralScope" :
                scope == CombatScope.Transmitter ? "tooltip.transmitterScope" : "tooltip.bothScopes");

        private static void AddAttackModifiers(
            TransmitterAttackModifierDefinitionSO[] modifiers,
            List<string> destination)
        {
            if (modifiers == null)
                return;

            for (int i = 0; i < modifiers.Length; i++)
            {
                TransmitterAttackModifierDefinitionSO modifier = modifiers[i];
                if (modifier is AddPenetrationAttackModifierDefinitionSO penetration)
                    destination.Add(L10n.Get("tooltip.penetration", FormatSigned(penetration.AdditionalPenetrationCount)));
                else if (modifier is AddProjectileCountAttackModifierDefinitionSO projectileCount)
                    destination.Add(L10n.Get("tooltip.projectiles", FormatSigned(projectileCount.AdditionalProjectileCount)));
                else if (modifier is MultiplyDamageAttackModifierDefinitionSO damage)
                    destination.Add(L10n.Get("tooltip.damageMultiplier", damage.DamageMultiplier.ToString("0.##", CultureInfo.InvariantCulture)));
                else if (modifier is AddDamageAttackModifierDefinitionSO damageBonus)
                    destination.Add(L10n.Get("tooltip.damageBonus", damageBonus.DamageAmount.ToString("0.##", CultureInfo.InvariantCulture)));
            }
        }

        /// <summary>按顺序显示有效技能及整卡共用的消费配置。</summary>
        private static void AddSkills(
            IReadOnlyList<SkillDefinitionSO> skills, SkillGrant grant, List<string> destination)
        {
            if (skills == null)
            {
                return;
            }

            var names = new List<string>();
            foreach (SkillDefinitionSO skill in skills)
            {
                if (skill != null)
                {
                    names.Add(string.IsNullOrWhiteSpace(skill.SkillIdentifier)
                        ? L10n.Get("tooltip.unnamedSkill")
                        : L10n.GetOrDefault($"skill.{skill.SkillIdentifier}.name", skill.SkillIdentifier));
                }
            }
            if (names.Count == 0)
            {
                return;
            }

            float cooldown = grant?.CooldownSeconds ?? 0f;
            float energy = grant?.EnergyCost ?? 0f;
            int uses = grant?.UseCount ?? 1;
            destination.Add(
                L10n.Get("tooltip.skills", string.Join(L10n.Get("tooltip.skillSeparator"), names),
                    cooldown.ToString("0.##", CultureInfo.InvariantCulture), energy.ToString("0.##", CultureInfo.InvariantCulture), uses));
        }

        /// <summary>格式化整数增量并保留正数的加号。</summary>
        private static string FormatSigned(int value)
            => value > 0 ? $"+{value}" : value.ToString(CultureInfo.InvariantCulture);
    }
}
