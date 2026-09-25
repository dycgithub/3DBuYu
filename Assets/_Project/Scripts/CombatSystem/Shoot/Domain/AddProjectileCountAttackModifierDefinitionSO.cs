using UnityEngine;
using UnityEngine.Serialization;

namespace CombatSystem
{
    /// <summary>增加每次攻击生成的投射物数量。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Modifiers/Add Projectile Count")]
    public sealed class AddProjectileCountAttackModifierDefinitionSO : TransmitterAttackModifierDefinitionSO
    {
        /// <summary>投射物数量增量。</summary>
        [FormerlySerializedAs("Amount")] public int AdditionalProjectileCount = 1;

        /// <summary>累加非负投射物数量。</summary>
        public override void Apply(ref TransmitterAttackModifiers modifiers)
        {
            modifiers.AdditionalProjectileCount += Mathf.Max(0, AdditionalProjectileCount);
        }
    }
}
