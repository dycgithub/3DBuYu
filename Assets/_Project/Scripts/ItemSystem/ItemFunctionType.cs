namespace ItemSystem
{
    /// <summary>
    /// 物品功能类型。
    ///   - Ammunition：弹药型，效果表达为一组 buff
    ///   - Skill：技能型，效果为具体行为（可用装饰器扩展）。
    /// </summary>
    public enum ItemFunctionType
    {
        Ammunition,
        Skill
    }
}
