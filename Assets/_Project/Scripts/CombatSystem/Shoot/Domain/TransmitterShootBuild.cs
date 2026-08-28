namespace CombatSystem
{
    public readonly struct TransmitterShootBuild
    {
        public BulletProfile Profile { get; }
        public float DamageBonus { get; }
        public float DamageMultiplier { get; }
        public float RangeMultiplier { get; }
        public int ProjectileCount { get; }
        public int Penetration { get; }
        public float CriticalChance { get; }
        public float CriticalDamage { get; }

        public TransmitterShootBuild(
            BulletProfile profile,
            float damageBonus,
            float damageMultiplier,
            float rangeMultiplier,
            int projectileCount,
            int penetration,
            float criticalChance,
            float criticalDamage)
        {
            Profile = profile;
            DamageBonus = damageBonus;
            DamageMultiplier = damageMultiplier;
            RangeMultiplier = rangeMultiplier;
            ProjectileCount = projectileCount;
            Penetration = penetration;
            CriticalChance = criticalChance;
            CriticalDamage = criticalDamage;
        }
    }
}
