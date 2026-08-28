using UnityEngine;

namespace CombatSystem
{
    [CreateAssetMenu(menuName = "Combat/Item Combat Definition")]
    public sealed class ItemCombatDefinition : ScriptableObject
    {
        [SerializeField] private CombatScope scope = CombatScope.CentralOrTransmitter;
        [Min(0f)] [SerializeField] private float transmitterDamageBonus = 50f;
        [SerializeField] private SkillDefinition centralSkill;
        [Min(0f)] [SerializeField] private float centralDamage = 50f;
        [SerializeField] private TransmitterShootModifierDefinition[] transmitterModifiers;

        private static ItemCombatDefinition _default;

        public CombatScope Scope => scope;
        public float TransmitterDamageBonus => transmitterDamageBonus;
        public SkillDefinition CentralSkill => centralSkill;
        public float CentralDamage => centralDamage;
        public TransmitterShootModifierDefinition[] TransmitterModifiers => transmitterModifiers;

        public bool AppliesToCentral => scope == CombatScope.CentralOrTransmitter || scope == CombatScope.Central;
        public bool AppliesToTransmitter => scope == CombatScope.CentralOrTransmitter || scope == CombatScope.Transmitter;

        public static ItemCombatDefinition Default
        {
            get
            {
                if (_default != null)
                    return _default;

                _default = CreateInstance<ItemCombatDefinition>();
                _default.name = "RuntimeItemCombatDefinition";
                _default.scope = CombatScope.CentralOrTransmitter;
                _default.transmitterDamageBonus = 50f;
                _default.centralDamage = 50f;
                _default.centralSkill = SkillDefinition.CreateRuntime("central-sweep", 50f);
                return _default;
            }
        }
    }
}
