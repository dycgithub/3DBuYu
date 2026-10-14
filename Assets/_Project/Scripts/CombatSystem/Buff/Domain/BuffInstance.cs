namespace CombatSystem
{
    /// <summary>保存单个 Buff 实例的定义引用、效果类型和生命周期数据。</summary>
    internal sealed class BuffInstance
    {
        internal const int DamageOverTimeStackLimit = 3;

        /// <summary>静态定义；程序创建的持续伤害实例不依赖 ScriptableObject。</summary>
        internal BuffDefinitionSO Definition { get; set; }

        /// <summary>标记由持续伤害入口直接创建的实例。</summary>
        internal bool IsDamageOverTime { get; set; }

        /// <summary>实例的属性类别；持续伤害实例固定为 DamageOverTime。</summary>
        internal BuffModifierType ModifierType => IsDamageOverTime
            ? BuffModifierType.DamageOverTime
            : Definition.ModifierType;

        /// <summary>实例的叠层策略；持续伤害实例始终独立叠加。</summary>
        internal BuffStackPolicy StackPolicy => IsDamageOverTime
            ? BuffStackPolicy.Independent
            : Definition.StackPolicy;

        /// <summary>实例的乘法值；持续伤害不改变属性倍率。</summary>
        internal float ModifierValue => IsDamageOverTime ? 1f : Definition.ModifierValue;

        /// <summary>实例的治疗速率；持续伤害实例没有治疗效果。</summary>
        internal float HealingPerSecond => Definition != null ? Definition.HealingPerSecond : 0f;

        /// <summary>剩余持续时间，单位为秒；非正持续时间表示永久状态。</summary>
        internal float RemainingDurationSeconds { get; set; }

        /// <summary>施加该 Buff 的来源实例标识。</summary>
        internal int SourceInstanceId { get; set; }

        /// <summary>当前层数，最小有效值为一层。</summary>
        internal int StackCount { get; set; } = 1;

        /// <summary>该独立持续伤害实例每秒造成的伤害。</summary>
        internal float DamagePerSecond { get; set; }

        internal int InstanceId { get; set; }
        internal int TargetGeneration { get; set; }
        internal IBuffTarget Target { get; set; }
        internal bool IsActive { get; set; }
        internal float ExpireAt { get; set; } = float.PositiveInfinity;
        internal float LastEffectTime { get; set; }
        internal int ExpiryQueueIndex { get; set; } = -1;
        internal int HealingListIndex { get; set; } = -1;
        internal int DamageListIndex { get; set; } = -1;
    }
}
