namespace CombatSystem
{
    /// <summary>
    /// BuffService 可登记的运行时目标。
    /// 目标只提供生命周期、抗性和属性变化通知，不保存任何 Buff 状态。
    /// </summary>
    public interface IBuffTarget
    {
        /// <summary>目标当前是否已经登记且可接收运行时效果。</summary>
        bool IsActive { get; }

        /// <summary>目标当前是否存活。</summary>
        bool IsAlive { get; }

        /// <summary>目标当前是否允许接收有害 Buff。</summary>
        bool CanReceiveHarmfulEffects { get; }

        /// <summary>
        /// 通知目标指定属性的 Buff 合成结果发生变化。
        /// </summary>
        /// <param name="modifierType">发生变化的属性类型。</param>
        void OnBuffModifiersChanged(BuffModifierType modifierType);
    }
}
