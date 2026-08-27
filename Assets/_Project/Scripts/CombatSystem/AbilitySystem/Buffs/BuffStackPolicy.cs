namespace CombatSystem
{
    /// <summary>同来源同类型 Buff 再次施加时的处理方式。</summary>
    public enum BuffStackPolicy
    {
        RefreshDuration,
        AddStack,
        Replace,
        Independent
    }
}
