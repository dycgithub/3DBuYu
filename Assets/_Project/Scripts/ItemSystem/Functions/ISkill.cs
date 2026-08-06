namespace ItemSystem.Functions
{
    /// <summary>
    /// 技能型物品功能：在公共抽象 IItemFunction 之上增加技能元信息。
    /// 技能的具体实现继承 SkillBase（核心技能）。
    /// 并可通过 SkillDecorator（装饰器模式）包裹扩展，装饰器本身也是 ISkill。
    /// </summary>
    public interface ISkill : IItemFunction
    {
        string Name { get; }
        string Description { get; }
    }
}
