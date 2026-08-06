using System;

namespace ItemSystem.Functions
{
    /// <summary>
    /// 技能修饰器抽象基类（装饰器模式）：
    /// 包装一个 ISkill，在激活前后附加行为。
    /// 子类可重写 BeforeActivate / AfterActivate 添加横切逻辑（日志、计费、通知等）。
    /// 或整体重写 OnActivate 改变执行流程（如 RepeatSkillDecorator 循环、CooldownSkillDecorator 限频）。
    /// 多个修饰器可层层嵌套组合出复杂技能。
    /// </summary>
    public abstract class SkillDecorator : SkillBase
    {
        /// <summary>被包装的底层技能。</summary>
        protected ISkill Inner { get; }

        protected SkillDecorator(ISkill inner)
        {
            Inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public override string Name => $"{GetType().Name}({Inner.Name})";

        protected override void OnActivate(IItemActivationContext context)
        {
            BeforeActivate(context);
            Inner.Activate(context);
            AfterActivate(context);
        }

        protected virtual void BeforeActivate(IItemActivationContext context) { }
        protected virtual void AfterActivate(IItemActivationContext context) { }
    }
}
