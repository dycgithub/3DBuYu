namespace ItemSystem.Functions
{
    /// <summary>
    /// 核心技能：瞬间消灭场上所有敌人。
    /// 可被 SkillDecorator 包裹扩展（冷却、连发等）。
    /// </summary>
    public class KillAllEnemiesSkill : SkillBase
    {
        public override string Name => "Kill All Enemies";
        public override string Description => "Instantaneously defeats every enemy currently on the field.";

        protected override void OnActivate(IItemActivationContext context) => context.KillAllEnemies();
    }
}
