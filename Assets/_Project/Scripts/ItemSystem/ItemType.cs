namespace ItemSystem
{
    /// <summary>
    /// 物品类型（道具系统的核心分类）：
    ///   - Skill：技能型（放入炮台插槽，通过 ISkill 功能实现）。
    ///   - Ammunition：弹药型（放入端口插槽，效果表达为一组 buff）。
    /// 存储系统的库存校验策略（ItemTypeValidator）据此决定物品可放入哪个网格。
    /// </summary>
    public enum ItemType
    {
        Skill = 0,
        Ammunition = 1
    }
}
