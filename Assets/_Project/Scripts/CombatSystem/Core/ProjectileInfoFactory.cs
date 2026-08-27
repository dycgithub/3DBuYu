using UnityEngine;

namespace CombatSystem
{
    /// <summary>把不同攻击入口的发射数据转换为统一投射物快照。</summary>
    public static class ProjectileInfoFactory
    {
        public static ProjectileInfo FromAttack(in AttackInfo attack)
        {
            return new ProjectileInfo
            {
                AttackId = attack.AttackId,
                SourceId = attack.SourceId,
                Profile = attack.Profile,
                Origin = attack.Origin,
                Direction = attack.Direction,
                Damage = attack.Damage,
                Speed = attack.Speed,
                MaxDistance = attack.MaxDistance,
                Radius = attack.Radius,
                Penetration = attack.Penetration,
                DamageType = attack.DamageType,
                IsCritical = attack.IsCritical
            };
        }

        public static ProjectileInfo Create(
            int attackId,
            int sourceId,
            BulletProfile profile,
            Vector3 origin,
            Vector3 direction,
            float damage,
            bool isCritical = false)
        {
            return new ProjectileInfo
            {
                AttackId = attackId,
                SourceId = sourceId,
                Profile = profile,
                Origin = origin,
                Direction = direction,
                Damage = damage,
                Speed = profile != null ? profile.Speed : 0f,
                MaxDistance = profile != null ? Mathf.Max(0.01f, profile.MaxDistance) : 0f,
                Radius = profile != null ? Mathf.Max(0f, profile.Radius) : 0f,
                Penetration = 0,
                DamageType = profile != null ? profile.DamageType : DamageType.Physical,
                IsCritical = isCritical
            };
        }
    }
}
