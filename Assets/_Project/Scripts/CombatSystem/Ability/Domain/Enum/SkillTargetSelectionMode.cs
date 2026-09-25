namespace CombatSystem
{
    /// <summary>自动目标规则；显式数值保持已有全体、最近单体和施法者范围配置的含义。</summary>
    public enum SkillTargetSelectionMode
    {
        AllLivingEnemies = 0,
        NearestEnemy = 2,
        AroundCaster = 3
    }
}
