namespace EnemySystem
{
    /// <summary>
    /// 敌人类型 — 仅 Normal / Fast / Tank 三种。
    /// 用于 ILockable 分类、积分与威胁等级,行为差异由组件承载。
    /// </summary>
    public enum EnemyType
    {
        Normal,
        Fast,
        Tank,
    }
}
