namespace EnemySystem
{
    /// <summary>敌人进入场景时所属的生成分类，与 EnemyType 的行为类型解耦。</summary>
    public enum EnemySpawnCategory
    {
        Normal,
        Elite,
        Boss,
        Child
    }
}
