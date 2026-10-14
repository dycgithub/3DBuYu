namespace CombatSystem
{
    /// <summary>追踪弹的搜索范围：普通发射器使用半径，星群核心搜索全场有效目标。</summary>
    public enum HomingTargetSearch
    {
        WithinRadius,
        AllLivingTargets
    }
}
