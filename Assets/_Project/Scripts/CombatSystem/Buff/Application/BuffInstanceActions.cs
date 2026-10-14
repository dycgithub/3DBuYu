namespace CombatSystem
{
    /// <summary>定义刷新时对周期效果起始时刻的处理方式。</summary>
    internal enum BuffRefreshMode
    {
        PreservePeriodicStart,
        ResetPeriodicStart
    }

    /// <summary>创建、刷新和移除 Buff 实例，并同步目标状态索引。</summary>
    internal sealed class BuffInstanceActions
    {
        private readonly BuffStateStore _stateStore;

        /// <summary>绑定用于分配实例和追踪目标代次的状态集合。</summary>
        internal BuffInstanceActions(BuffStateStore stateStore)
        {
            _stateStore = stateStore;
        }

        /// <summary>创建实例并加入目标状态。</summary>
        internal BuffInstance Create(
            BuffTargetState state,
            BuffDefinitionSO definition,
            int sourceInstanceId,
            float currentTime)
        {
            BuffInstance instance = _stateStore.CreateInstance(
                state,
                definition,
                sourceInstanceId,
                currentTime);
            state.AddInstance(instance);
            state.MarkModifierChanged(definition.ModifierType);
            return instance;
        }

        /// <summary>创建不依赖静态资产的独立持续伤害实例。</summary>
        internal BuffInstance CreateDamageOverTime(
            BuffTargetState state,
            int sourceInstanceId,
            float damagePerSecond,
            float durationSeconds,
            float currentTime)
        {
            BuffInstance instance = _stateStore.CreateDamageOverTimeInstance(
                state,
                sourceInstanceId,
                damagePerSecond,
                durationSeconds,
                currentTime);
            state.AddInstance(instance);
            state.MarkModifierChanged(instance.ModifierType);
            return instance;
        }

        /// <summary>刷新实例时长和绝对到期时刻。</summary>
        internal void Refresh(
            BuffInstance instance,
            BuffDefinitionSO durationDefinition,
            float currentTime)
        {
            float durationSeconds = durationDefinition.DurationSeconds;
            instance.RemainingDurationSeconds = durationSeconds;
            instance.ExpireAt = durationSeconds > 0f
                ? currentTime + durationSeconds
                : float.PositiveInfinity;
        }

        /// <summary>移除实例，并通知目标重新合成对应属性。</summary>
        internal void Remove(BuffTargetState state, BuffInstance instance)
        {
            if (instance == null || !instance.IsActive)
            {
                return;
            }

            RemoveFromState(state, instance);
            state.MarkModifierChanged(instance.ModifierType);
        }

        /// <summary>清除期间移除实例，延后统一通知目标。</summary>
        internal void RemoveWithoutNotification(BuffTargetState state, BuffInstance instance)
        {
            RemoveFromState(state, instance);
        }

        private void RemoveFromState(BuffTargetState state, BuffInstance instance)
        {
            if (instance == null || !instance.IsActive)
            {
                return;
            }

            _stateStore.RemoveInstance(instance);
            state.RemoveInstance(instance);
            instance.IsActive = false;
            if (!float.IsPositiveInfinity(instance.ExpireAt))
            {
                instance.RemainingDurationSeconds = 0f;
            }
        }
    }
}
