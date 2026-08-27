using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 把端口、子弹资产和装备修改器合成为一次攻击快照。
    /// </summary>
    public sealed class AttackBuilder
    {
        private readonly IAttackModifierSource _modifierSource;
        private readonly AttackModifierPipeline _modifierPipeline;
        private readonly List<IAttackModifier> _modifiers = new();

        public AttackBuilder(
            IAttackModifierSource modifierSource,
            AttackModifierPipeline modifierPipeline)
        {
            _modifierSource = modifierSource;
            _modifierPipeline = modifierPipeline;
        }

        public bool TryBuild(
            in PortAttackContext context,
            out AttackInfo attack)
        {
            attack = default;
            BulletProfile profile = context.Profile;
            if (profile == null)
                return false;

            Vector3 direction = context.Direction;
            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.forward;

            float damage = Mathf.Max(0f, profile.Damage);
            float maxDistance = Mathf.Max(0.01f, profile.MaxDistance);
            damage *= Mathf.Max(0f, context.DamageMultiplier);
            maxDistance *= Mathf.Max(0f, context.RangeMultiplier);

            int projectileCount = Mathf.Max(1, context.ProjectileCount);
            int penetration = Mathf.Max(0, context.Penetration);
            bool isCritical = Random.value < Mathf.Clamp01(context.CriticalChance);
            if (isCritical)
                damage *= Mathf.Max(1f, context.CriticalDamage);

            attack = new AttackInfo
            {
                AttackId = 0,
                SourceId = context.SourceId,
                PortIndex = context.PortIndex,
                Profile = profile,
                Origin = context.Origin,
                Direction = direction,
                Damage = damage,
                EnergyCost = Mathf.Max(0f, profile.EnergyCost),
                ProjectileCount = projectileCount,
                Penetration = penetration,
                DamageType = profile.DamageType,
                Speed = Mathf.Max(0f, profile.Speed),
                MaxDistance = maxDistance,
                Radius = Mathf.Max(0f, profile.Radius),
                IsCritical = isCritical
            };

            _modifierSource?.CollectAttackModifiers(context.PortIndex, _modifiers);
            _modifierPipeline?.Apply(ref attack, _modifiers);
            return true;
        }
    }
}
