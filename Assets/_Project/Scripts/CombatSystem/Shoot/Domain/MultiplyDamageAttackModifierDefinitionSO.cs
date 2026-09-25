using UnityEngine;
using UnityEngine.Serialization;

namespace CombatSystem
{
    /// <summary>按配置倍数缩放发射器攻击伤害。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Modifiers/Multiply Damage")]
    public sealed class MultiplyDamageAttackModifierDefinitionSO : TransmitterAttackModifierDefinitionSO
    {
        /// <summary>伤害倍率。</summary>
        [FormerlySerializedAs("Multiplier")] [Min(0f)] public float DamageMultiplier = 1f;

        /// <summary>将非负伤害倍率乘入本次攻击。</summary>
        public override void Apply(ref TransmitterAttackModifiers modifiers)
        {
            modifiers.DamageMultiplier *= Mathf.Max(0f, DamageMultiplier);
        }
    }
}
