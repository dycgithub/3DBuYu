namespace CombatSystem
{
    public sealed class TransmitterShootBuildService
    {
        private readonly ITransmitterShootModifierSource _modifierSource;

        public TransmitterShootBuildService(ITransmitterShootModifierSource modifierSource)
        {
            _modifierSource = modifierSource;
        }

        public TransmitterShootBuild Build(in TransmitterAttackInput input)
        {
            TransmitterShootModifiers modifiers = TransmitterShootModifiers.Default;
            _modifierSource?.Collect(input.TransmitterIndex, ref modifiers);
            modifiers.Clamp();

            BulletProfile profile = input.Profile != null ? input.Profile : BulletProfile.Default;
            return new TransmitterShootBuild(
                profile,
                modifiers.DamageBonus,
                UnityEngine.Mathf.Max(0f, input.DamageMultiplier) * modifiers.DamageMultiplier,
                UnityEngine.Mathf.Max(0f, input.RangeMultiplier) * modifiers.RangeMultiplier,
                UnityEngine.Mathf.Max(1, input.ProjectileCount + modifiers.ProjectileCountBonus),
                UnityEngine.Mathf.Max(0, input.Penetration + modifiers.PenetrationBonus),
                UnityEngine.Mathf.Clamp01(input.CriticalChance + modifiers.CriticalChanceBonus),
                UnityEngine.Mathf.Max(1f, input.CriticalDamage * modifiers.CriticalDamageMultiplier));
        }
    }
}
