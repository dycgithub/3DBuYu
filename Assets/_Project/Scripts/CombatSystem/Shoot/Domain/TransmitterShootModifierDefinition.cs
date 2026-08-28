using UnityEngine;

namespace CombatSystem
{
    public abstract class TransmitterShootModifierDefinition : ScriptableObject
    {
        public abstract void Apply(ref TransmitterShootModifiers modifiers);
    }

    [CreateAssetMenu(menuName = "Combat/Shoot Modifiers/Add Damage")]
    public sealed class AddDamageShootModifierDefinition : TransmitterShootModifierDefinition
    {
        public float Amount = 50f;

        public override void Apply(ref TransmitterShootModifiers modifiers)
        {
            modifiers.DamageBonus += Mathf.Max(0f, Amount);
        }
    }

    [CreateAssetMenu(menuName = "Combat/Shoot Modifiers/Multiply Damage")]
    public sealed class MultiplyDamageShootModifierDefinition : TransmitterShootModifierDefinition
    {
        [Min(0f)] public float Multiplier = 1f;

        public override void Apply(ref TransmitterShootModifiers modifiers)
        {
            modifiers.DamageMultiplier *= Mathf.Max(0f, Multiplier);
        }
    }

    [CreateAssetMenu(menuName = "Combat/Shoot Modifiers/Add Projectile Count")]
    public sealed class AddProjectileCountShootModifierDefinition : TransmitterShootModifierDefinition
    {
        public int Amount = 1;

        public override void Apply(ref TransmitterShootModifiers modifiers)
        {
            modifiers.ProjectileCountBonus += Mathf.Max(0, Amount);
        }
    }

    [CreateAssetMenu(menuName = "Combat/Shoot Modifiers/Add Penetration")]
    public sealed class AddPenetrationShootModifierDefinition : TransmitterShootModifierDefinition
    {
        public int Amount = 1;

        public override void Apply(ref TransmitterShootModifiers modifiers)
        {
            modifiers.PenetrationBonus += Mathf.Max(0, Amount);
        }
    }
}
