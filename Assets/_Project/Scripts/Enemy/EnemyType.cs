namespace EnemySystem
{
    /// <summary>
    /// 敌人类型。
    /// 用于静态配置、Flocking Profile 和锁定分类；具体机制由预制体组件组合承载。
    /// </summary>
    public enum EnemyType
    {
        // 普通敌人
        Normal = 0,
        Fast = 1,
        Tank = 2,
        Reflect = 5,
        Exploder = 6,

        // 精英敌人
        Slow = 8,
        Healer = 9,
        StrongHealer = 10,

        // Boss：Assault、Shooter 使用行为树；Growth、Mother 使用 GOAP。
        Assault = 3,
        Shooter = 7,
        Growth = 4,
        Mother = 11
    }
}
