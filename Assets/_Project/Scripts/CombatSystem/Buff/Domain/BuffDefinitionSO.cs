using UnityEngine;
using UnityEngine.Serialization;

namespace CombatSystem
{
    /// <summary>Buff 的正负效果分类。</summary>
    public enum BuffPolarity
    {
        Harmful,
        Beneficial,
        Neutral
    }

    /// <summary>
    /// Buff 的静态配置资产，不保存目标、来源或层数等运行时状态。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Buff Definition")]
    public sealed class BuffDefinitionSO : ScriptableObject
    {
        /// <summary>该 Buff 修改的属性类别。</summary>
        [FormerlySerializedAs("Type")]
        public BuffModifierType ModifierType;

        /// <summary>
        /// 可选的叠层组标识。合并键由来源、ModifierType 和叠层组共同组成。
        /// 为空时仅按相同来源与 ModifierType 合并。
        /// </summary>
        public string StackGroupId;

        /// <summary>该 Buff 是否属于有害效果。</summary>
        public BuffPolarity Polarity = BuffPolarity.Harmful;

        /// <summary>持续时间，单位为秒；小于等于零表示永久。</summary>
        [FormerlySerializedAs("Duration")]
        public float DurationSeconds;

        /// <summary>当前实现用于乘法修正的数值。</summary>
        [FormerlySerializedAs("Value")]
        public float ModifierValue;

        /// <summary>持续治疗 Buff 每秒恢复的生命或等价生存资源。</summary>
        [Min(0f)]
        public float HealingPerSecond;

        /// <summary>
        /// 同来源、同 ModifierType 且同 StackGroupId 的 Buff 重复施加策略；组标识为空时按来源与 ModifierType 合并。
        /// </summary>
        public BuffStackPolicy StackPolicy = BuffStackPolicy.RefreshDuration;

        /// <summary>该 Buff 允许达到的最大层数。</summary>
        [FormerlySerializedAs("MaxStacks")]
        [Min(1)] public int MaximumStackCount = 1;

        /// <summary>可选的 Buff 持续视效；活动期间附着于目标，结束时归还对象池。</summary>
        public GameObject VisualEffectPrefab;
    }
}
