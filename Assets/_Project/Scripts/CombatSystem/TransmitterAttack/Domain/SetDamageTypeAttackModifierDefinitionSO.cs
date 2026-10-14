using UnityEngine;

namespace CombatSystem
{
    /// <summary>覆盖发射器攻击最终提交给伤害管线的伤害类型。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Modifiers/Set Damage Type")]
    public sealed class SetDamageTypeAttackModifierDefinitionSO : TransmitterAttackModifierDefinitionSO
    {
        /// <summary>要覆盖成的伤害类型。</summary>
        public DamageType DamageType = DamageType.Pure;

        /// <summary>记录本次攻击的伤害类型覆盖值。</summary>
        public override void Apply(ref TransmitterAttackModifiers modifiers)
        {
            modifiers.DamageTypeOverride = DamageType;
        }
    }
}
