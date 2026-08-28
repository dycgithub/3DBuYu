using UnityEngine;

namespace CombatSystem
{
    [CreateAssetMenu(menuName = "Combat/Ability Definition")]
    public sealed class SkillDefinition : ScriptableObject
    {
        [SerializeField] private string skillId;
        [Min(0f)] [SerializeField] private float cooldown;
        [Min(0f)] [SerializeField] private float energyCost;
        [Min(0f)] [SerializeField] private float damage = 50f;
        [SerializeField] private DamageType damageType = DamageType.Physical;
        [SerializeField] private SkillTargetMode targetMode = SkillTargetMode.AllLivingEnemies;
        [SerializeField] private bool requiresTargetPointer = true;

        public string SkillId => skillId;
        public float Cooldown => cooldown;
        public float EnergyCost => energyCost;
        public float Damage => damage;
        public DamageType DamageType => damageType;
        public SkillTargetMode TargetMode => targetMode;
        public bool RequiresTargetPointer => requiresTargetPointer;

        public static SkillDefinition CreateRuntime(string id, float damageAmount)
        {
            SkillDefinition definition = CreateInstance<SkillDefinition>();
            definition.name = string.IsNullOrEmpty(id) ? "RuntimeAbility" : id;
            definition.skillId = id;
            definition.damage = Mathf.Max(0f, damageAmount);
            definition.energyCost = 0f;
            definition.targetMode = SkillTargetMode.AllLivingEnemies;
            definition.requiresTargetPointer = true;
            return definition;
        }
    }
}
