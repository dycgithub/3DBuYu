using UnityEngine;
namespace CombatSystem
{
    /// <summary>
    /// 技能的静态逻辑配置资产，保存标识、目标、伤害和动作规则；物品定义负责卡牌表现。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Ability Definition")]
    public sealed class SkillDefinitionSO : ScriptableObject
    {
        [Header("身份")]
        [SerializeField] private string skillIdentifier;
        [Header("目标")]
        [SerializeField] private SkillTargetSelectionMode targetSelectionMode = SkillTargetSelectionMode.AllLivingEnemies;
        [Min(0f)] [SerializeField] private float targetRadius;
        [SerializeField] private bool allowNoTarget;

        [Header("默认动作")]
        [Min(0f)] [SerializeField] private float damageAmount = 50f;
        [SerializeField] private DamageType damageType = DamageType.Physical;
        [SerializeField] private SkillActionDefinitionSO actionDefinition;

        /// <summary>获取技能的稳定标识。</summary>
        public string SkillIdentifier => skillIdentifier;

        /// <summary>获取技能造成的伤害量。</summary>
        public float DamageAmount => damageAmount;

        /// <summary>获取技能伤害类型。</summary>
        public DamageType DamageType => damageType;

        /// <summary>获取技能的目标选择模式。</summary>
        public SkillTargetSelectionMode TargetSelectionMode => targetSelectionMode;

        /// <summary>获取范围目标的半径。</summary>
        public float TargetRadius => targetRadius;

        /// <summary>指示技能是否允许没有目标时释放。</summary>
        public bool AllowNoTarget => allowNoTarget;

        /// <summary>获取可选的自定义动作定义。</summary>
        public SkillActionDefinitionSO ActionDefinition => actionDefinition;

        /// <summary>
        /// 创建未保存到工程的运行时技能定义。
        /// </summary>
        /// <param name="skillIdentifier">运行时技能标识。</param>
        /// <param name="damageAmount">默认技能伤害量，负值会被限制为零。</param>
        /// <returns>新的运行时技能配置对象。</returns>
        /// <remarks>返回对象由调用方负责生命周期管理；其余配置使用安全默认值。</remarks>
        public static SkillDefinitionSO CreateRuntimeDefinition(string skillIdentifier, float damageAmount)
        {
            SkillDefinitionSO definition = CreateInstance<SkillDefinitionSO>();
            definition.name = string.IsNullOrEmpty(skillIdentifier) ? "TemporaryAbility" : skillIdentifier;
            definition.skillIdentifier = skillIdentifier;
            definition.damageAmount = Mathf.Max(0f, damageAmount);
            definition.targetSelectionMode = SkillTargetSelectionMode.AllLivingEnemies;
            definition.allowNoTarget = false;
            return definition;
        }
    }
}
