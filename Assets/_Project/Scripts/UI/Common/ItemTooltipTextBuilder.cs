using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CombatSystem;
using UnityEngine;

namespace _Project.UI.Common
{
    /// <summary>
    /// 把 ItemVM 和战斗授予配置转换为 Tooltip 展示数据。
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
            CombatItemGrant grant = definition.CombatGrant;
            if (grant != null)
            {
                scope = FormatScope(grant.Scope);
                AddAttackModifiers(grant.AttackModifiers, effects);
                AddSkills(grant.SkillGrants, effects);
                AddBuffs(grant.EquipBuffs, effects);
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

        private static string FormatScope(EquipmentScope scope)
            => scope == EquipmentScope.Turret ? "作用范围：炮台" : "作用范围：炮口";

        private static void AddAttackModifiers(
            AttackModifierDefinitionSO[] modifiers,
            List<string> destination)
        {
            if (modifiers == null)
                return;

            for (int i = 0; i < modifiers.Length; i++)
            {
                AttackModifierDefinitionSO modifier = modifiers[i];
                if (modifier is AddPenetrationAttackModifierSO penetration)
                {
                    destination.Add($"攻击穿透 {FormatSigned(penetration.amount)}");
                }
                else if (modifier is AddProjectileCountAttackModifierSO projectileCount)
                {
                    destination.Add($"额外弹丸 {FormatSigned(projectileCount.amount)}");
                }
                else if (modifier is MultiplyDamageAttackModifier damage)
                {
                    destination.Add($"攻击伤害 ×{damage.multiplier.ToString("0.##", CultureInfo.InvariantCulture)}");
                }
            }
        }

        private static void AddSkills(SkillDefinition[] skills, List<string> destination)
        {
            if (skills == null)
                return;

            for (int i = 0; i < skills.Length; i++)
            {
                SkillDefinition skill = skills[i];
                if (skill == null)
                    continue;

                string name = string.IsNullOrWhiteSpace(skill.SkillId) ? "未命名技能" : skill.SkillId;
                destination.Add(
                    $"获得技能：{name}（冷却 {skill.Cooldown.ToString("0.##", CultureInfo.InvariantCulture)} 秒，能量 {skill.EnergyCost.ToString("0.##", CultureInfo.InvariantCulture)}）");
            }
        }

        private static void AddBuffs(BuffConfig[] buffs, List<string> destination)
        {
            if (buffs == null)
                return;

            for (int i = 0; i < buffs.Length; i++)
            {
                BuffConfig buff = buffs[i];
                if (buff == null)
                    continue;

                string duration = buff.Duration > 0f
                    ? $"，持续 {buff.Duration.ToString("0.##", CultureInfo.InvariantCulture)} 秒"
                    : "，常驻";
                destination.Add(
                    $"{FormatBuffType(buff.Type)} ×{buff.Value.ToString("0.##", CultureInfo.InvariantCulture)}{duration}");
            }
        }

        private static string FormatBuffType(BuffType type)
            => type switch
            {
                BuffType.DamageTakenMultiplier => "受到伤害倍率",
                BuffType.SpeedMultiplier => "移动速度倍率",
                BuffType.DamageResistance => "伤害抗性",
                BuffType.AttackDamage => "攻击伤害",
                BuffType.Range => "攻击射程",
                BuffType.FireRate => "攻击射速",
                BuffType.ProjectileCount => "弹丸数量",
                BuffType.CriticalChance => "暴击率",
                BuffType.CriticalDamage => "暴击伤害",
                BuffType.Bounce => "弹射次数",
                _ => "战斗效果",
            };

        private static string FormatSigned(int value)
            => value > 0 ? $"+{value}" : value.ToString(CultureInfo.InvariantCulture);
    }
}
