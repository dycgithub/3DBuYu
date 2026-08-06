using System;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// 波次触发器 — 决定当前波次何时开始。
    /// 设计目标:支持「时间」(前波清场后延迟)与「手动」(代码/按钮显式调用 StartNextWave)。
    /// </summary>
    [Serializable]
    public class WaveTrigger
    {
        [Tooltip("触发类型")]
        public WaveTriggerType type = WaveTriggerType.PreviousCleared;

        [Tooltip("前波清场后延迟多少秒开始(仅 PreviousCleared 类型生效)")]
        public float delayAfterPrevious = 0f;
    }

    public enum WaveTriggerType
    {
        /// <summary>前一波清场(敌人全部消灭)后,延迟 delayAfterPrevious 秒自动开始。</summary>
        PreviousCleared,

        /// <summary>不自动开始 — 需代码显式调用 IWaveEventService.StartNextWave()。</summary>
        Manual,
    }
}
