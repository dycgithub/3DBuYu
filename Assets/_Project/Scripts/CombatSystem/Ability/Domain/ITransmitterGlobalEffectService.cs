namespace CombatSystem
{
    /// <summary>
    /// 保存可由发射器攻击系统消费的全局效果状态。
    /// </summary>
    public interface ITransmitterGlobalEffectService
    {
        /// <summary>当前全局发射器射速倍率。</summary>
        float AttackSpeedMultiplier { get; }

        /// <summary>设置一个持续到状态清除的全局射速倍率。</summary>
        void SetAttackSpeedMultiplier(float multiplier, int sourceInstanceId);
    }
}
