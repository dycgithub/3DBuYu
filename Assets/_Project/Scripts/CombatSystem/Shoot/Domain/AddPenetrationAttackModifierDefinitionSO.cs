using UnityEngine;
using UnityEngine.Serialization;

namespace CombatSystem
{
    /// <summary>增加每颗投射物可穿透的目标次数。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Modifiers/Add Penetration")]
    public sealed class AddPenetrationAttackModifierDefinitionSO : TransmitterAttackModifierDefinitionSO
    {
        /// <summary>穿透次数增量。</summary>
        [FormerlySerializedAs("Amount")] public int AdditionalPenetrationCount = 1;

        /// <summary>累加非负穿透次数。</summary>
        public override void Apply(ref TransmitterAttackModifiers modifiers)
        {
            modifiers.AdditionalPenetrationCount += Mathf.Max(0, AdditionalPenetrationCount);
        }
    }
}
