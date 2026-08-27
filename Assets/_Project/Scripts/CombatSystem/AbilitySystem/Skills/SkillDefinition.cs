using UnityEngine;

namespace CombatSystem
{
    /// <summary>技能的静态资产配置，运行时状态由 SkillRuntime 保存。</summary>
    [CreateAssetMenu(menuName = "ShootingSystem/Skill Definition")]
    public sealed class SkillDefinition : ScriptableObject
    {
        [SerializeField] private string skillId;
        [Min(0f)] [SerializeField] private float cooldown;
        [Min(0f)] [SerializeField] private float energyCost;
        [SerializeField] private SkillActionDefinition[] actions;

        public string SkillId => skillId;
        public float Cooldown => cooldown;
        public float EnergyCost => energyCost;
        public SkillActionDefinition[] Actions => actions;
    }
}
