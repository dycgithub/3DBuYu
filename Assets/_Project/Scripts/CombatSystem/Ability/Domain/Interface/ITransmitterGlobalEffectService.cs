using Interfaces;

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
        void SetAttackSpeedMultiplier(float multiplier, int sourceInstanceId, float durationSeconds);

        /// <summary>保存一个限时发射器集火目标。</summary>
        void SetFocusedTarget(IDamageable target, float durationSeconds, int sourceInstanceId);

        /// <summary>获取当前仍存活且未过期的集火目标。</summary>
        IDamageable GetFocusedTarget();
    }
}
