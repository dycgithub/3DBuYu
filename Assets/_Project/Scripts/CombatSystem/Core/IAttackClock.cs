namespace CombatSystem
{
    /// <summary>攻击系统读取当前时间的最小端口。</summary>
    public interface IAttackClock
    {
        float Time { get; }
    }
}
