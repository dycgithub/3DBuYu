namespace ItemSystem.Functions
{
    /// <summary>
    /// 技能抽象基类：实现公共抽象 IItemFunction（FunctionType = Skill），
    /// 子类只需实现 OnActivate 定义技能的核心效果。
    /// </summary>
    public abstract class SkillBase : ISkill
    {
        public virtual string Name => GetType().Name;
        public virtual string Description => string.Empty;
        public ItemFunctionType FunctionType => ItemFunctionType.Skill;

        public void Activate(IItemActivationContext context) => OnActivate(context);

        protected abstract void OnActivate(IItemActivationContext context);
    }
}
