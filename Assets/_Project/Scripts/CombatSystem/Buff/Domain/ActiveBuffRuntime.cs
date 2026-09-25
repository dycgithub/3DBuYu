using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 保存一个已施加 Buff 的运行时状态。
    /// </summary>
    public sealed class ActiveBuffRuntime
    {
        /// <summary>创建该状态的静态 Buff 定义。</summary>
        public BuffDefinitionSO Definition { get; set; }

        /// <summary>剩余持续时间，单位为秒；非正持续时间表示永久状态。</summary>
        public float RemainingDurationSeconds { get; set; }

        /// <summary>施加该 Buff 的来源实例标识。</summary>
        public int SourceInstanceId { get; set; }

        /// <summary>当前层数，最小有效值为一层。</summary>
        public int StackCount { get; set; } = 1;

        /// <summary>该独立持续伤害实例每秒造成的伤害。</summary>
        public float DamagePerSecond { get; set; }

        // 以下字段由场景级 BuffManner 使用；它们不属于 BuffDefinitionSO 的静态配置。
        internal int RuntimeId { get; set; }
        internal int TargetGeneration { get; set; }
        internal IBuffTarget Target { get; set; }
        internal bool IsActive { get; set; }
        internal bool UsesManagedExpiry { get; set; }
        internal float ExpireAt { get; set; } = float.PositiveInfinity;
        internal float LastEffectTime { get; set; }
        internal int ExpiryQueueIndex { get; set; } = -1;
        internal int HealingListIndex { get; set; } = -1;
        internal int DamageListIndex { get; set; } = -1;

        /// <summary>
        /// 判断该 Buff 是否已经到期。
        /// </summary>
        /// <value>存在定义且为限时 Buff、剩余时间不大于零时返回 <c>true</c>。</value>
        public bool IsExpired => Definition != null &&
            (UsesManagedExpiry
                ? !float.IsPositiveInfinity(ExpireAt) && RemainingDurationSeconds <= 0f
                : Definition.DurationSeconds > 0f && RemainingDurationSeconds <= 0f);

        /// <summary>
        /// 按给定时间推进 Buff，并返回本次仍处于有效期内的时间。
        /// </summary>
        /// <param name="deltaTime">经过的秒数；负值按零处理。</param>
        /// <returns>本次可以执行持续效果的有效秒数。</returns>
        /// <remarks>永久 Buff 不会减少剩余时间，但仍返回完整的推进时间。</remarks>
        public float AdvanceTime(float deltaTime)
        {
            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            if (Definition == null)
            {
                return 0f;
            }

            if (Definition.DurationSeconds <= 0f)
            {
                return safeDeltaTime;
            }

            float activeTime = Mathf.Min(
                safeDeltaTime,
                Mathf.Max(0f, RemainingDurationSeconds));
            RemainingDurationSeconds -= safeDeltaTime;
            return activeTime;
        }
    }
}
