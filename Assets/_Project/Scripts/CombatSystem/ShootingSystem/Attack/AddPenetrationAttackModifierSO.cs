using UnityEngine;

namespace CombatSystem
{
    /// <summary>穿透力</summary>
    [CreateAssetMenu(menuName = "ShootingSystem/Attack Modifiers/Add Penetration")]
    public sealed class AddPenetrationAttackModifierSO : AttackModifierDefinitionSO
    {
        public int amount = 1;

        public override void Modify(ref AttackInfo info)
        {
            info.Penetration = Mathf.Max(0, info.Penetration + amount);
        }
    }
}
