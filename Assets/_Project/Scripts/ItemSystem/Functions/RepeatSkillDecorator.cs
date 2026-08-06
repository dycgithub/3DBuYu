using System;

namespace ItemSystem.Functions
{
    /// <summary>
    /// 重复修饰器：连续激活被包裹技能 N 次（如连发/多重施放）。
    /// 示例：new RepeatSkillDecorator(new UnlockAllPortsSkill(), 3)
    /// </summary>
    public class RepeatSkillDecorator : SkillDecorator
    {
        private readonly int _times;

        public RepeatSkillDecorator(ISkill inner, int times) : base(inner)
        {
            _times = Math.Max(1, times);
        }

        public override string Name => $"{Inner.Name} x{_times}";

        protected override void OnActivate(IItemActivationContext context)
        {
            for (int i = 0; i < _times; i++)
                Inner.Activate(context);
        }
    }
}
