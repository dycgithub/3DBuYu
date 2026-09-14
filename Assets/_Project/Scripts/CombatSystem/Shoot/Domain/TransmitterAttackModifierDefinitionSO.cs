using UnityEngine;
using UnityEngine.Serialization;

namespace CombatSystem
{
    /// <summary>
    /// 定义会修改发射器攻击参数的可配置策略。
    /// </summary>
    public abstract class TransmitterAttackModifierDefinitionSO : ScriptableObject
    {
        /// <summary>
        /// 将当前配置应用到攻击修正值。
        /// </summary>
        /// <param name="modifiers">要修改的攻击修正值。</param>
        public abstract void Apply(ref TransmitterAttackModifiers modifiers);
    }

    /// <summary>为攻击增加固定伤害量。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Modifiers/Add Damage")]
    public sealed class AddDamageAttackModifierDefinitionSO : TransmitterAttackModifierDefinitionSO
    {
        /// <summary>要增加的伤害量。</summary>
        [FormerlySerializedAs("Amount")]
        public float DamageAmount = 50f;

        /// <summary>将非负固定伤害增量累加到修正值。</summary>
        public override void Apply(ref TransmitterAttackModifiers modifiers)
        {
            modifiers.AdditiveDamageBonus += Mathf.Max(0f, DamageAmount);
        }
    }

    /// <summary>按配置倍数缩放攻击伤害。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Modifiers/Multiply Damage")]
    public sealed class MultiplyDamageAttackModifierDefinitionSO : TransmitterAttackModifierDefinitionSO
    {
        /// <summary>伤害乘数。</summary>
        [FormerlySerializedAs("Multiplier")]
        [Min(0f)] public float DamageMultiplier = 1f;

        /// <summary>将非负伤害乘数乘入修正值。</summary>
        public override void Apply(ref TransmitterAttackModifiers modifiers)
        {
            modifiers.DamageMultiplier *= Mathf.Max(0f, DamageMultiplier);
        }
    }

    /// <summary>按配置倍数缩放端口每秒攻击次数。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Modifiers/Multiply Attack Speed")]
    public sealed class MultiplyAttackSpeedAttackModifierDefinitionSO : TransmitterAttackModifierDefinitionSO
    {
        /// <summary>每秒攻击次数乘数。</summary>
        [Min(0f)] public float AttackSpeedMultiplier = 3f;

        /// <summary>将非负射速乘数乘入修正值。</summary>
        public override void Apply(ref TransmitterAttackModifiers modifiers)
        {
            modifiers.AttackSpeedMultiplier *= Mathf.Max(0f, AttackSpeedMultiplier);
        }
    }

    /// <summary>按配置倍数缩放投射物碰撞半径。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Modifiers/Multiply Collision Radius")]
    public sealed class MultiplyCollisionRadiusAttackModifierDefinitionSO : TransmitterAttackModifierDefinitionSO
    {
        /// <summary>碰撞半径乘数。</summary>
        [Min(0f)] public float CollisionRadiusMultiplier = 2f;

        /// <summary>将非负碰撞半径乘数乘入修正值。</summary>
        public override void Apply(ref TransmitterAttackModifiers modifiers)
        {
            modifiers.CollisionRadiusMultiplier *= Mathf.Max(0f, CollisionRadiusMultiplier);
        }
    }

    /// <summary>覆盖投射物最终提交给伤害管线的伤害类型。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Modifiers/Set Damage Type")]
    public sealed class SetDamageTypeAttackModifierDefinitionSO : TransmitterAttackModifierDefinitionSO
    {
        /// <summary>要提交的伤害类型。</summary>
        public DamageType DamageType = DamageType.Pure;

        /// <summary>记录最终伤害类型覆盖值。</summary>
        public override void Apply(ref TransmitterAttackModifiers modifiers)
        {
            modifiers.DamageTypeOverride = DamageType;
        }
    }

    /// <summary>增加一次攻击生成的投射物数量。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Modifiers/Add Projectile Count")]
    public sealed class AddProjectileCountAttackModifierDefinitionSO : TransmitterAttackModifierDefinitionSO
    {
        /// <summary>要增加的投射物数量。</summary>
        [FormerlySerializedAs("Amount")]
        public int AdditionalProjectileCount = 1;

        /// <summary>将非负投射物数量增量累加到修正值。</summary>
        public override void Apply(ref TransmitterAttackModifiers modifiers)
        {
            modifiers.AdditionalProjectileCount += Mathf.Max(0, AdditionalProjectileCount);
        }
    }

    /// <summary>增加一次攻击的投射物穿透次数。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Modifiers/Add Penetration")]
    public sealed class AddPenetrationAttackModifierDefinitionSO : TransmitterAttackModifierDefinitionSO
    {
        /// <summary>要增加的穿透次数。</summary>
        [FormerlySerializedAs("Amount")]
        public int AdditionalPenetrationCount = 1;

        /// <summary>将非负穿透增量累加到修正值。</summary>
        public override void Apply(ref TransmitterAttackModifiers modifiers)
        {
            modifiers.AdditionalPenetrationCount += Mathf.Max(0, AdditionalPenetrationCount);
        }
    }
}
