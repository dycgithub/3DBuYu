using UnityEngine;

namespace CombatSystem
{
    public readonly struct TransmitterAttackInput
    {
        public int SourceId { get; }
        public int TransmitterIndex { get; }
        public BulletProfile Profile { get; }
        public Vector3 Origin { get; }
        public Vector3 Direction { get; }
        public float FireRate { get; }
        public float DamageMultiplier { get; }
        public float RangeMultiplier { get; }
        public int ProjectileCount { get; }
        public int Penetration { get; }
        public float CriticalChance { get; }
        public float CriticalDamage { get; }

        public TransmitterAttackInput(
            int sourceId,
            int transmitterIndex,
            BulletProfile profile,
            Vector3 origin,
            Vector3 direction,
            float fireRate,
            float damageMultiplier,
            float rangeMultiplier,
            int projectileCount,
            int penetration,
            float criticalChance,
            float criticalDamage)
        {
            SourceId = sourceId;
            TransmitterIndex = transmitterIndex;
            Profile = profile;
            Origin = origin;
            Direction = direction;
            FireRate = fireRate;
            DamageMultiplier = damageMultiplier;
            RangeMultiplier = rangeMultiplier;
            ProjectileCount = projectileCount;
            Penetration = penetration;
            CriticalChance = criticalChance;
            CriticalDamage = criticalDamage;
        }
    }
}
