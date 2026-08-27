using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 炮台适配层提交给攻击系统的中立输入快照。
    /// 不包含炮台场景对象，避免攻击系统反向依赖 Play 模块。
    /// </summary>
    public readonly struct PortAttackContext
    {
        public int SourceId { get; }
        public int PortIndex { get; }
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

        public PortAttackContext(
            int sourceId,
            int portIndex,
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
            PortIndex = portIndex;
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
