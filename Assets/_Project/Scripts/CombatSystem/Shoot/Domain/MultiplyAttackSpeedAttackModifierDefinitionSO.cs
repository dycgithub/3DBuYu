using UnityEngine;

namespace CombatSystem
{
    /// <summary>按配置倍数缩放发射器每秒攻击次数。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Modifiers/Multiply Attack Speed")]
    public sealed class MultiplyAttackSpeedAttackModifierDefinitionSO : TransmitterAttackModifierDefinitionSO
    {
        /// <summary>每秒攻击次数倍率。</summary>
        [Min(0f)] public float AttackSpeedMultiplier = 1.5f;

        /// <summary>将非负射速倍率乘入本次攻击。</summary>
        public override void Apply(ref TransmitterAttackModifiers modifiers)
        {
            modifiers.AttackSpeedMultiplier *= Mathf.Max(0f, AttackSpeedMultiplier);
        }
    }
}
