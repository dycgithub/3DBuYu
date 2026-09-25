using System;

namespace CombatSystem
{
    /// <summary>发布所有实际扣血结果的战斗事件源。</summary>
    public interface IDamageEventSource
    {
        /// <summary>目标实际扣除生命后触发。</summary>
        event Action<DamageEvent> DamageApplied;
    }
}
