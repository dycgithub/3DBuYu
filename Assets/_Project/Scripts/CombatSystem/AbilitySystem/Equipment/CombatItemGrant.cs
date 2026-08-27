using UnityEngine;

namespace CombatSystem
{
    /// <summary>物品提供给战斗系统的技能、攻击修改和装备 Buff。</summary>
    [CreateAssetMenu(menuName = "ShootingSystem/Equipment/Combat Item Grant")]
    public sealed class CombatItemGrant : ScriptableObject
    {
        [SerializeField] private EquipmentScope scope = EquipmentScope.Port;
        [SerializeField] private AttackModifierDefinitionSO[] attackModifiers;
        [SerializeField] private SkillDefinition[] skillGrants;
        [SerializeField] private BuffConfig[] equipBuffs;

        public EquipmentScope Scope => scope;
        public AttackModifierDefinitionSO[] AttackModifiers => attackModifiers;
        public SkillDefinition[] SkillGrants => skillGrants;
        public BuffConfig[] EquipBuffs => equipBuffs;
    }
}
