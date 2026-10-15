using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 物品在战斗系统中的静态配置，定义作用范围、技能授予、发射器修正与专属弹体外观。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Item Combat Definition")]
    public sealed class ItemCombatDefinitionSO : ScriptableObject
    {
        [SerializeField] private CombatScope combatScope = CombatScope.CentralAndTransmitter;
        [Min(0f)] [SerializeField] private float transmitterAdditiveDamageBonus;
        [SerializeField] private ProjectileProfileSO transmitterProjectileProfile;
        [SerializeField] private ProjectileVisualDefinitionSO transmitterProjectileVisualDefinition;
        [SerializeField] private SkillGrant centralSkillGrant;
        [SerializeField] private TransmitterAttackModifierDefinitionSO[] transmitterAttackModifierDefinitions;
        [Min(0f)] [SerializeField] private float transmitterEnergyCostMultiplier = 1f;
        [Min(0f)] [SerializeField] private float centralEnergyCostMultiplier = 1f;

        private static ItemCombatDefinitionSO _runtimeFallback;

        public CombatScope CombatScope => combatScope;
        public float TransmitterAdditiveDamageBonus => transmitterAdditiveDamageBonus;
        public ProjectileProfileSO TransmitterProjectileProfile => transmitterProjectileProfile;
        /// <summary>发射器道具提供的独立弹体视觉；不包含投射物机制。</summary>
        public ProjectileVisualDefinitionSO TransmitterProjectileVisualDefinition
            => transmitterProjectileVisualDefinition;
        public SkillGrant CentralSkillGrant => centralSkillGrant;
        public TransmitterAttackModifierDefinitionSO[] TransmitterAttackModifierDefinitions
            => transmitterAttackModifierDefinitions;
        public float TransmitterEnergyCostMultiplier => Mathf.Max(0f, transmitterEnergyCostMultiplier);
        public float CentralEnergyCostMultiplier => Mathf.Max(0f, centralEnergyCostMultiplier);

        public bool AppliesToCentralLoadout
            => combatScope == CombatScope.CentralAndTransmitter || combatScope == CombatScope.Central;

        public bool AppliesToTransmitterLoadout
            => combatScope == CombatScope.CentralAndTransmitter || combatScope == CombatScope.Transmitter;

        /// <summary>获取没有额外技能授予的共享回退配置。</summary>
        public static ItemCombatDefinitionSO RuntimeFallback
        {
            get
            {
                if (_runtimeFallback != null)
                {
                    return _runtimeFallback;
                }

                _runtimeFallback = CreateInstance<ItemCombatDefinitionSO>();
                _runtimeFallback.name = "RuntimeItemCombatDefinition";
                _runtimeFallback.combatScope = CombatScope.CentralAndTransmitter;
                _runtimeFallback.transmitterAdditiveDamageBonus = 50f;
                return _runtimeFallback;
            }
        }

        /// <summary>创建未保存的战斗配置，供测试和占位配置使用。</summary>
        public static ItemCombatDefinitionSO CreateRuntime(
            CombatScope scope,
            SkillGrant skillGrant)
        {
            ItemCombatDefinitionSO definition = CreateInstance<ItemCombatDefinitionSO>();
            definition.name = "RuntimeItemCombatDefinition";
            definition.combatScope = scope;
            definition.centralSkillGrant = skillGrant;
            return definition;
        }
    }
}
