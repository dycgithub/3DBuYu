
namespace ItemSystem.Functions
{
    /// <summary>
    /// 物品功能的公共抽象（弹药与技能的共同接口）。
    /// 两类实现：
    ///   - AmmunitionFunction：弹药 = 一组 buff（与 ShootingSystem.Buffs 结合）。
    ///   - ISkill（SkillBase / SkillDecorator）：技能 = 具体行为，可用装饰器扩展
    /// 激活时通过 IItemActivationContext 执行对应的游戏内操作。
    /// </summary>
    public interface IItemFunction
    {
        /// <summary>功能类型：弹药 / 技能。</summary>
        ItemFunctionType FunctionType { get; }

        /// <summary>激活物品功能。</summary>
        void Activate(IItemActivationContext context);
    }
}
