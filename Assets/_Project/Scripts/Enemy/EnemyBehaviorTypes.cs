namespace EnemySystem
{
    /// <summary>敌人的死亡来源，用于区分正常击杀和吞噬清理。</summary>
    public enum EnemyDeathCause
    {
        None,
        Damage,
        Forced,
        Consumed,
        PlayerContact,
        ParentDied
    }

    /// <summary>敌人死亡时是否参与奖励和击杀统计。</summary>
    public enum EnemyRewardMode
    {
        Award,
        Ignore
    }

    /// <summary>范围效果组件的行为模式。</summary>
    public enum EnemyAreaEffectType
    {
        Slow,
        HealAll,
        HealEnemies
    }

    /// <summary>进攻敌人的状态机状态。</summary>
    public enum EnemyAssaultState
    {
        Roaming,
        Turning,
        Charging,
        Dashing
    }

    /// <summary>Boss 的单向战斗阶段。</summary>
    public enum EnemyBossPhase
    {
        First,
        Second
    }
}
