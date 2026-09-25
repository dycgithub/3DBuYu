using UnityEngine;

namespace CombatSystem
{
    /// <summary>按配置倍数缩放投射物碰撞半径。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Modifiers/Multiply Collision Radius")]
    public sealed class MultiplyCollisionRadiusAttackModifierDefinitionSO : TransmitterAttackModifierDefinitionSO
    {
        /// <summary>碰撞半径倍率。</summary>
        [Min(0f)] public float CollisionRadiusMultiplier = 2f;

        /// <summary>将非负半径倍率乘入本次攻击。</summary>
        public override void Apply(ref TransmitterAttackModifiers modifiers)
        {
            modifiers.CollisionRadiusMultiplier *= Mathf.Max(0f, CollisionRadiusMultiplier);
        }
    }
}
