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

        /// <summary>
        /// 判断该 Buff 是否已经到期。
        /// </summary>
        /// <value>存在定义且为限时 Buff、剩余时间不大于零时返回 <c>true</c>。</value>
        public bool IsExpired => Definition != null && Definition.DurationSeconds > 0f && RemainingDurationSeconds <= 0f;

        /// <summary>
        /// 按给定时间推进 Buff 的剩余持续时间。
        /// </summary>
        /// <param name="deltaTime">经过的秒数；负值按零处理。</param>
        /// <remarks>永久 Buff 不会减少剩余时间。</remarks>
        public void AdvanceTime(float deltaTime)
        {
            if (Definition != null && Definition.DurationSeconds > 0f)
                RemainingDurationSeconds -= Mathf.Max(0f, deltaTime);
        }
    }
}
