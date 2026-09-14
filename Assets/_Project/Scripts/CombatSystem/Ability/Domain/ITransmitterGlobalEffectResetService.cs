namespace CombatSystem
{
    /// <summary>清理中央装备状态产生的发射器全局效果。</summary>
    public interface ITransmitterGlobalEffectResetService
    {
        /// <summary>清除当前全局效果并恢复默认状态。</summary>
        void ClearAll();
    }
}
