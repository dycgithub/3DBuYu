using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CombatSystem;
using UnityEngine;

namespace _Project.UI.Common
{
    /// <summary>
    /// 把 ItemVM 和战斗配置转换为 Tooltip 展示数据。
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
                    effects.Add($"发射器伤害 +{grant.TransmitterAdditiveDamageBonus.ToString("0.##", CultureInfo.InvariantCulture)}");
                }
            }

            string footprint = item.Width > 0 && item.Height > 0
                ? $"占用：{item.Width}×{item.Height} 格，共 {item.CoordinateSet.Count} 格 · 方向 {GridUtilities.RotationHelper.GetRotationAngle(item.Direction)}°"
                : "占用：无";

            bool hasPrice = gridType == GridType.Shop;
            string priceText = hasPrice
                ? price <= 0 ? "免费" : $"{price} 积分"
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
            => scope == CombatScope.Central ? "作用范围：中心" :
                scope == CombatScope.Transmitter ? "作用范围：发射器" : "作用范围：中心/发射器";

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
                    destination.Add($"攻击穿透 {FormatSigned(penetration.AdditionalPenetrationCount)}");
                else if (modifier is AddProjectileCountAttackModifierDefinitionSO projectileCount)
                    destination.Add($"额外弹丸 {FormatSigned(projectileCount.AdditionalProjectileCount)}");
                else if (modifier is MultiplyDamageAttackModifierDefinitionSO damage)
                    destination.Add($"攻击伤害 ×{damage.DamageMultiplier.ToString("0.##", CultureInfo.InvariantCulture)}");
                else if (modifier is AddDamageAttackModifierDefinitionSO damageBonus)
                    destination.Add($"攻击伤害 +{damageBonus.DamageAmount.ToString("0.##", CultureInfo.InvariantCulture)}");
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
                    names.Add(string.IsNullOrWhiteSpace(skill.SkillIdentifier) ? "未命名技能" : skill.SkillIdentifier);
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
                $"获得技能：{string.Join("、", names)}（冷却 {cooldown.ToString("0.##", CultureInfo.InvariantCulture)} 秒，能量 {energy.ToString("0.##", CultureInfo.InvariantCulture)}，次数 {uses}）");
        }

        /// <summary>格式化整数增量并保留正数的加号。</summary>
        private static string FormatSigned(int value)
            => value > 0 ? $"+{value}" : value.ToString(CultureInfo.InvariantCulture);
    }
}
