using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 汇总一个发射器端口装备产生的攻击修正值。
    /// </summary>
    public struct TransmitterAttackModifiers
    {
        /// <summary>最终伤害上的加法增量。</summary>
        public float AdditiveDamageBonus;

        /// <summary>最终伤害乘数。</summary>
        public float DamageMultiplier;

        /// <summary>最大旅行距离乘数。</summary>
        public float RangeMultiplier;

        /// <summary>每秒攻击次数乘数。</summary>
        public float AttackSpeedMultiplier;

        /// <summary>投射物碰撞半径乘数。</summary>
        public float CollisionRadiusMultiplier;

        /// <summary>额外发射的投射物数量。</summary>
        public int AdditionalProjectileCount;

        /// <summary>额外的穿透次数。</summary>
        public int AdditionalPenetrationCount;

        /// <summary>额外的暴击概率。</summary>
        public float AdditionalCriticalChance;

        /// <summary>暴击伤害乘数。</summary>
        public float CriticalDamageMultiplier;

        /// <summary>覆盖投射物配置的伤害类型；为空时沿用投射物配置。</summary>
        public DamageType? DamageTypeOverride;

        /// <summary>覆盖发射器默认投射物配置；为空时沿用发射器配置。</summary>
        public ProjectileProfileSO ProjectileProfileOverride;

        /// <summary>获取不改变输入攻击的默认修正值。</summary>
        public static TransmitterAttackModifiers Default => new()
        {
            DamageMultiplier = 1f,
            RangeMultiplier = 1f,
            AttackSpeedMultiplier = 1f,
            CollisionRadiusMultiplier = 1f,
            CriticalDamageMultiplier = 1f
        };

        /// <summary>将所有修正值限制到攻击构建器可处理的有效范围。</summary>
        public void ClampToValidRanges()
        {
            AdditiveDamageBonus = Mathf.Max(0f, AdditiveDamageBonus);
            DamageMultiplier = Mathf.Max(0f, DamageMultiplier);
            RangeMultiplier = Mathf.Max(0f, RangeMultiplier);
            AttackSpeedMultiplier = Mathf.Max(0f, AttackSpeedMultiplier);
            CollisionRadiusMultiplier = Mathf.Max(0f, CollisionRadiusMultiplier);
            AdditionalProjectileCount = Mathf.Max(0, AdditionalProjectileCount);
            AdditionalPenetrationCount = Mathf.Max(0, AdditionalPenetrationCount);
            AdditionalCriticalChance = Mathf.Max(0f, AdditionalCriticalChance);
            CriticalDamageMultiplier = Mathf.Max(0f, CriticalDamageMultiplier);
        }
    }

    /// <summary>
    /// 向攻击构建器提供指定发射器端口的缓存装备修正值。
    /// </summary>
    public interface ITransmitterAttackModifierSource
    {
        /// <summary>
        /// 收集指定端口的攻击修正值。
        /// </summary>
        /// <param name="portId">发射器稳定标识。</param>
        /// <param name="modifiers">要累加的攻击修正值。</param>
        void CollectAttackModifiers(string portId, ref TransmitterAttackModifiers modifiers);
    }

}
