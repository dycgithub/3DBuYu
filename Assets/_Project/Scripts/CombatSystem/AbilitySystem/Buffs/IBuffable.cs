namespace CombatSystem
{
    /// <summary>接收并管理攻击效果施加的 Buff 的目标接口。</summary>
    public interface IBuffable
    {
        void ApplyBuff(BuffConfig config);
        void ApplyBuff(BuffConfig config, int sourceId);
    }
}
