namespace CombatSystem
{
    /// <summary>
    /// 定义同一来源的同类 Buff 再次施加时的合并策略。
    /// </summary>
    public enum BuffStackPolicy
    {
        /// <summary>保留当前实例并刷新持续时间。</summary>
        RefreshDuration = 0,

        /// <summary>增加层数并刷新持续时间。</summary>
        AddStack = 1,

        /// <summary>用新定义替换当前实例。</summary>
        Replace = 2,

        /// <summary>无论已有实例如何都创建独立实例。</summary>
        Independent = 3
    }
}
