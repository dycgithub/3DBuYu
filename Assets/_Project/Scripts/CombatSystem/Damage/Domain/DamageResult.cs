using UnityEngine.Serialization;

namespace CombatSystem
{
    /// <summary>
    /// 描述目标处理伤害请求后的结果。
    /// </summary>
    public struct DamageResult
    {
        /// <summary>目标处理请求时产生的结果状态。</summary>
        public DamageOutcome Outcome;

        /// <summary>实际从目标生命值中扣除的伤害量。</summary>
        [FormerlySerializedAs("ActualDamage")]
        public float AppliedDamage;

        /// <summary>处理完成后目标剩余生命值。</summary>
        public float RemainingHealth;

        /// <summary>指示这次请求是否使目标进入死亡状态。</summary>
        [FormerlySerializedAs("IsKill")]
        public bool WasKilled;

        /// <summary>
        /// 指示请求是否实际造成了伤害。
        /// </summary>
        /// <value>当结果为已应用或已击杀时返回 <c>true</c>。</value>
        public bool WasApplied => Outcome == DamageOutcome.Applied || Outcome == DamageOutcome.Killed;

        /// <summary>
        /// 指示伤害是否被完全闪避。
        /// </summary>
        /// <value>仅当结果状态为 <see cref="DamageOutcome.Dodged"/> 时返回 <c>true</c>。</value>
        public bool WasDodged => Outcome == DamageOutcome.Dodged;
    }
}
