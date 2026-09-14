using System;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// 敌群触发器，决定当前敌群何时开始。
    /// 支持前一敌群清场后的延迟和代码显式触发。
    /// </summary>
    [Serializable]
    public class PopulationTrigger
    {
        [Tooltip("触发类型")]
        public PopulationTriggerType type = PopulationTriggerType.PreviousCleared;

        [Tooltip("前一敌群清场后延迟多少秒开始(仅 PreviousCleared 类型生效)")]
        public float delayAfterPrevious = 0f;
    }

    public enum PopulationTriggerType
    {
        /// <summary>前一敌群清场(敌人全部消灭)后,延迟 delayAfterPrevious 秒自动开始。</summary>
        PreviousCleared,

        /// <summary>不自动开始，由代码显式触发。</summary>
        Manual,
    }
}
