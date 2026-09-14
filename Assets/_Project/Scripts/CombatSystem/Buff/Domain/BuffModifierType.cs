namespace CombatSystem
{
    /// <summary>
    /// 表示 Buff 修改的属性类别。
    /// </summary>
    public enum BuffModifierType
    {
        /// <summary>承受伤害倍率。</summary>
        DamageTakenMultiplier = 0,

        /// <summary>移动速度倍率。</summary>
        MovementSpeedMultiplier = 1,

        /// <summary>伤害抗性修正。</summary>
        DamageResistance = 2,

        /// <summary>攻击伤害修正。</summary>
        AttackDamage = 3,

        /// <summary>攻击范围修正。</summary>
        AttackRange = 4,

        /// <summary>攻击频率修正。</summary>
        FireRate = 5,

        /// <summary>投射物数量修正。</summary>
        ProjectileCount = 6,

        /// <summary>暴击概率修正。</summary>
        CriticalChance = 7,

        /// <summary>暴击伤害修正。</summary>
        CriticalDamage = 8,

        /// <summary>投射物弹跳次数修正。</summary>
        ProjectileBounceCount = 9
    }
}
