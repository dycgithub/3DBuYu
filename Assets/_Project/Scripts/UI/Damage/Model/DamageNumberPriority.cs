namespace _Project.UI.Damage
{
    /// <summary>伤害跳字的显示优先级；数值越大越不容易被裁剪。</summary>
    public enum DamageNumberPriority
    {
        Normal = 0,
        DamageOverTime = 1,
        WeakPoint = 2,
        Critical = 3,
        Boss = 4
    }
}
