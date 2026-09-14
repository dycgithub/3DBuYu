using CombatSystem;

namespace Interfaces
{
    /// <summary>描述可受战斗系统影响的目标阵营。</summary>
    public enum CombatTargetKind
    {
        Enemy,
        Player
    }

    /// <summary>为目标提供统一的阵营标识。</summary>
    public interface ICombatTarget : IDamageable
    {
        CombatTargetKind TargetKind { get; }
    }

    /// <summary>为范围效果提供统一的治疗入口。</summary>
    public interface IHealable
    {
        /// <summary>恢复生命或等价生存资源，并返回实际恢复量。</summary>
        float Heal(float amount);
    }

    /// <summary>表示目标当前是否拒绝有害控制或状态效果。</summary>
    public interface IControlResistant
    {
        bool CanReceiveHarmfulEffects { get; }
    }

    /// <summary>玩家战斗目标的组合契约。</summary>
    public interface IPlayerCombatTarget : ICombatTarget, IHealable, IBuffReceiver, IDamageRequestReceiver
    {
    }

}
