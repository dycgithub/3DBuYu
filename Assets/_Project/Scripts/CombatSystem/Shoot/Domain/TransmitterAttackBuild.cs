namespace CombatSystem
{
    /// <summary>
    /// 保存一个端口在生成最终攻击数据前汇总出的参数。
    /// </summary>
    public readonly struct TransmitterAttackBuild
    {
        /// <summary>本次攻击使用的投射物配置。</summary>
        public ProjectileProfileSO ProjectileProfile { get; }

        /// <summary>攻击基础伤害上的加法增量。</summary>
        public float AdditiveDamageBonus { get; }

        /// <summary>攻击伤害乘数。</summary>
        public float DamageMultiplier { get; }

        /// <summary>攻击最大距离乘数。</summary>
        public float RangeMultiplier { get; }

        /// <summary>本次攻击每秒允许的攻击次数。</summary>
        public float AttacksPerSecond { get; }

        /// <summary>本次攻击使用的最终碰撞半径。</summary>
        public float CollisionRadius { get; }

        /// <summary>本次攻击使用的最终伤害类型。</summary>
        public DamageType DamageType { get; }

        /// <summary>本次攻击生成的投射物数量。</summary>
        public int ProjectileCount { get; }

        /// <summary>本次攻击的穿透次数。</summary>
        public int PenetrationCount { get; }

        /// <summary>本次攻击的暴击概率。</summary>
        public float CriticalChance { get; }

        /// <summary>本次攻击的暴击伤害乘数。</summary>
        public float CriticalDamageMultiplier { get; }

        /// <summary>创建汇总后的攻击参数。</summary>
        /// <param name="projectileProfile">本次攻击使用的投射物配置。</param>
        /// <param name="additiveDamageBonus">攻击伤害加法增量。</param>
        /// <param name="damageMultiplier">攻击伤害乘数。</param>
        /// <param name="rangeMultiplier">攻击最大距离乘数。</param>
        /// <param name="attacksPerSecond">本次攻击每秒允许的攻击次数。</param>
        /// <param name="collisionRadius">本次攻击使用的最终碰撞半径。</param>
        /// <param name="damageType">本次攻击使用的最终伤害类型。</param>
        /// <param name="projectileCount">本次攻击生成的投射物数量。</param>
        /// <param name="penetrationCount">本次攻击的穿透次数。</param>
        /// <param name="criticalChance">本次攻击的暴击概率。</param>
        /// <param name="criticalDamageMultiplier">暴击伤害乘数。</param>
        public TransmitterAttackBuild(
            ProjectileProfileSO projectileProfile,
            float additiveDamageBonus,
            float damageMultiplier,
            float rangeMultiplier,
            float attacksPerSecond,
            float collisionRadius,
            DamageType damageType,
            int projectileCount,
            int penetrationCount,
            float criticalChance,
            float criticalDamageMultiplier)
        {
            ProjectileProfile = projectileProfile;
            AdditiveDamageBonus = additiveDamageBonus;
            DamageMultiplier = damageMultiplier;
            RangeMultiplier = rangeMultiplier;
            AttacksPerSecond = attacksPerSecond;
            CollisionRadius = collisionRadius;
            DamageType = damageType;
            ProjectileCount = projectileCount;
            PenetrationCount = penetrationCount;
            CriticalChance = criticalChance;
            CriticalDamageMultiplier = criticalDamageMultiplier;
        }
    }
}
