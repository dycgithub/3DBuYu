using CombatSystem;

namespace Services
{
    /// <summary>向游戏逻辑提供玩家已保存的发射器攻击模式。</summary>
    public interface ITransmitterAttackModeProvider
    {
        /// <summary>当前发射器攻击模式。</summary>
        TransmitterAttackMode AttackMode { get; }
    }
}
