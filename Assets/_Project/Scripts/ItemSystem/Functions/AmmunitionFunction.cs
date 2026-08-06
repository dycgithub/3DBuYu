using System.Collections.Generic;
using ShootingSystem.Buffs;
using UnityEngine;

namespace ItemSystem.Functions
{
    /// <summary>
    /// 弹药型物品功能：与 buff 系统结合——弹药的效果统一表达为一组 BuffConfig。
    /// 激活时通过 IItemActivationContext.ApplyAmmunitionBuffs 施加到玩家炮台上
    /// （炮塔 BuffController 负责挂载/计时/刷新，射击系统经 GetModifier 读取数值）。
    /// </summary>
    public class AmmunitionFunction : IItemFunction
    {
        /// <summary>该弹药产生的 buff 列表（攻击力、射程、暴击、弹射等）。</summary>
        public IReadOnlyList<BuffConfig> Buffs { get; }

        public AmmunitionFunction(IReadOnlyList<BuffConfig> buffs)
        {
            Buffs = buffs ?? System.Array.Empty<BuffConfig>();
        }

        public ItemFunctionType FunctionType => ItemFunctionType.Ammunition;

        public void Activate(IItemActivationContext context)
        {
            context.ApplyAmmunitionBuffs(Buffs);
        }
    }

    /// <summary>
    /// 工厂：把 ItemConfig 上的弹药数值字段转换为 AmmunitionFunction 与 buff 列表。
    /// 仅非零/有效的数值才会生成对应 BuffConfig。
    /// </summary>
    public static class AmmunitionFunctionFactory
    {
        public static AmmunitionFunction Create(ItemConfig config)
        {
            if (config == null || config.ItemType != ItemType.Ammunition)
                return new AmmunitionFunction(System.Array.Empty<BuffConfig>());

            var buffs = new List<BuffConfig>();

            if (config.attackBonus > 0f)
                buffs.Add(CreateBuff(BuffType.AttackDamage, config.attackBonus));
            if (config.rangeBonus > 0f)
                buffs.Add(CreateBuff(BuffType.Range, config.rangeBonus));
            if (config.criticalChanceBonus > 0f)
                buffs.Add(CreateBuff(BuffType.CriticalChance, config.criticalChanceBonus));
            if (config.criticalDamageBonus > 0f)
                buffs.Add(CreateBuff(BuffType.CriticalDamage, config.criticalDamageBonus));
            if (config.isBounce)
                buffs.Add(CreateBuff(BuffType.Bounce, config.bounceCount));

            return new AmmunitionFunction(buffs);
        }

        private static BuffConfig CreateBuff(BuffType type, float value)
        {
            var buff = ScriptableObject.CreateInstance<BuffConfig>();
            buff.Type = type;
            buff.Value = value;
            buff.Duration = 0f; // 0 = 常驻，直到被替换/移除
            return buff;
        }
    }
}
