namespace EnemySystem
{
    /// <summary>
    /// 敌人类型。
    /// 用于静态配置、Flocking Profile 和锁定分类；具体机制由预制体组件组合承载。
    /// </summary>
    public enum EnemyType
    {
        Normal = 0,
        Fast = 1,
        Tank = 2,
        Assault = 3,
        Growth = 4,
        Reflect = 5,
        Exploder = 6,
        Shooter = 7,
        Slow = 8,
        Healer = 9,
        StrongHealer = 10,
        Mother = 11
    }
}
