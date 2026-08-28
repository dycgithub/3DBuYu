using UnityEngine;

namespace CombatSystem
{
    public sealed class AttackBuilder
    {
        public bool TryBuild(
            in TransmitterAttackInput input,
            in TransmitterShootBuild build,
            out AttackInfo attack)
        {
            attack = default;
            if (build.Profile == null)
                return false;

            Vector3 direction = input.Direction.sqrMagnitude > 0.0001f
                ? input.Direction.normalized
                : Vector3.forward;

            float damage = Mathf.Max(0f, build.Profile.Damage + build.DamageBonus);
            damage *= build.DamageMultiplier;

            bool isCritical = Random.value < build.CriticalChance;
            if (isCritical)
                damage *= build.CriticalDamage;

            attack = new AttackInfo
            {
                SourceId = input.SourceId,
                TransmitterIndex = input.TransmitterIndex,
                Profile = build.Profile,
                Origin = input.Origin,
                Direction = direction,
                Damage = Mathf.Max(0f, damage),
                EnergyCost = Mathf.Max(0f, build.Profile.EnergyCost),
                ProjectileCount = Mathf.Max(1, build.ProjectileCount),
                Penetration = Mathf.Max(0, build.Penetration),
                DamageType = build.Profile.DamageType,
                Speed = Mathf.Max(0f, build.Profile.Speed),
                MaxDistance = Mathf.Max(0.01f, build.Profile.MaxDistance * build.RangeMultiplier),
                Radius = Mathf.Max(0f, build.Profile.Radius),
                IsCritical = isCritical
            };
            return true;
        }
    }
}
