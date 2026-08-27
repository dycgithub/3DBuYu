namespace CombatSystem
{
    /// <summary>
    /// Buff 类型。前 3 项为敌人侧 debuff；
    /// 其余为玩家/弹药侧 buff，数值由 BuffConfig.Value 提供，
    /// 消费方（炮台属性 / 射击系统）经 BuffController.GetModifier(BuffType) 读取。
    /// </summary>
    public enum BuffType
    {
        // === 敌人侧 debuff ===
        DamageTakenMultiplier,
        SpeedMultiplier,
        DamageResistance,

        // === 玩家/弹药侧 buff（AmmunitionFunction 产生） ===
        AttackDamage,
        Range,
        FireRate,
        ProjectileCount,
        CriticalChance,
        CriticalDamage,
        Bounce,
    }
}
