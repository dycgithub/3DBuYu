using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CombatSystem;
using UnityEngine;

namespace _Project.UI.Common
{
    /// <summary>
    /// 把 ItemVM 和战斗配置转换为 Tooltip 展示数据。
    /// 这里只格式化当前已知的效果类型，不把内部资产名称暴露给玩家。
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
                return null;

            var effects = new List<string>();
            string scope = string.Empty;
            ItemCombatDefinition grant = definition.CombatDefinition ?? ItemCombatDefinition.Default;
            if (grant != null)
            {
                scope = FormatScope(grant.Scope);
                AddAttackModifiers(grant.TransmitterModifiers, effects);
                AddSkill(grant.CentralSkill, effects);
                if (grant.AppliesToTransmitter)
                    effects.Add($"发射器伤害 +{grant.TransmitterDamageBonus.ToString("0.##", CultureInfo.InvariantCulture)}");
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

        private static string FormatScope(CombatScope scope)
            => scope == CombatScope.Central ? "作用范围：中心" :
                scope == CombatScope.Transmitter ? "作用范围：发射器" : "作用范围：中心/发射器";

        private static void AddAttackModifiers(
            TransmitterShootModifierDefinition[] modifiers,
            List<string> destination)
        {
            if (modifiers == null)
                return;

            for (int i = 0; i < modifiers.Length; i++)
            {
                TransmitterShootModifierDefinition modifier = modifiers[i];
                if (modifier is AddPenetrationShootModifierDefinition penetration)
                    destination.Add($"攻击穿透 {FormatSigned(penetration.Amount)}");
                else if (modifier is AddProjectileCountShootModifierDefinition projectileCount)
                    destination.Add($"额外弹丸 {FormatSigned(projectileCount.Amount)}");
                else if (modifier is MultiplyDamageShootModifierDefinition damage)
                    destination.Add($"攻击伤害 ×{damage.Multiplier.ToString("0.##", CultureInfo.InvariantCulture)}");
                else if (modifier is AddDamageShootModifierDefinition damageBonus)
                    destination.Add($"攻击伤害 +{damageBonus.Amount.ToString("0.##", CultureInfo.InvariantCulture)}");
            }
        }

        private static void AddSkill(SkillDefinition skill, List<string> destination)
        {
            if (skill == null)
                return;

            string name = string.IsNullOrWhiteSpace(skill.SkillId) ? "未命名技能" : skill.SkillId;
            destination.Add(
                $"获得技能：{name}（冷却 {skill.Cooldown.ToString("0.##", CultureInfo.InvariantCulture)} 秒，能量 {skill.EnergyCost.ToString("0.##", CultureInfo.InvariantCulture)}）");
        }

        private static string FormatSigned(int value)
            => value > 0 ? $"+{value}" : value.ToString(CultureInfo.InvariantCulture);
    }
}
