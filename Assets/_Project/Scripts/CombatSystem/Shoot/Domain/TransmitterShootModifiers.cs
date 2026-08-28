using UnityEngine;

namespace CombatSystem
{
    public struct TransmitterShootModifiers
    {
        public float DamageBonus;
        public float DamageMultiplier;
        public float RangeMultiplier;
        public int ProjectileCountBonus;
        public int PenetrationBonus;
        public float CriticalChanceBonus;
        public float CriticalDamageMultiplier;

        public static TransmitterShootModifiers Default => new()
        {
            DamageMultiplier = 1f,
            RangeMultiplier = 1f,
            CriticalDamageMultiplier = 1f
        };

        public void Clamp()
        {
            DamageBonus = Mathf.Max(0f, DamageBonus);
            DamageMultiplier = Mathf.Max(0f, DamageMultiplier);
            RangeMultiplier = Mathf.Max(0f, RangeMultiplier);
            ProjectileCountBonus = Mathf.Max(0, ProjectileCountBonus);
            PenetrationBonus = Mathf.Max(0, PenetrationBonus);
            CriticalChanceBonus = Mathf.Max(0f, CriticalChanceBonus);
            CriticalDamageMultiplier = Mathf.Max(0f, CriticalDamageMultiplier);
        }
    }

    public interface ITransmitterShootModifierSource
    {
        void Collect(int transmitterIndex, ref TransmitterShootModifiers modifiers);
    }
}
