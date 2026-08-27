using UnityEngine;

namespace CombatSystem
{
    /// <summary>按倍率修改本次攻击伤害。</summary>
    [CreateAssetMenu(menuName = "ShootingSystem/Attack Modifiers/Multiply Damage")]
    public sealed class MultiplyDamageAttackModifier : AttackModifierDefinitionSO
    {
        [Min(0f)] public float multiplier = 1f;

        public override void Modify(ref AttackInfo info)
        {
            info.Damage = Mathf.Max(0f, info.Damage * Mathf.Max(0f, multiplier));
        }
    }
}
