using UnityEngine;
using UnityEngine.Serialization;

namespace CombatSystem
{
    /// <summary>为每次攻击增加固定伤害量。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Modifiers/Add Damage")]
    public sealed class AddDamageAttackModifierDefinitionSO : TransmitterAttackModifierDefinitionSO
    {
        /// <summary>伤害增量。</summary>
        [FormerlySerializedAs("Amount")] public float DamageAmount = 50f;

        /// <summary>累加非负伤害增量。</summary>
        public override void Apply(ref TransmitterAttackModifiers modifiers)
        {
            modifiers.AdditiveDamageBonus += Mathf.Max(0f, DamageAmount);
        }
    }
}
