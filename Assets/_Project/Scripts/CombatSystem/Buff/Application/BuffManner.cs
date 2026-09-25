using System;
using System.Collections.Generic;
using Interfaces;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

namespace CombatSystem
{
    /// <summary>
    /// 场景级 Buff 运行时管理器。
    /// <para>所有敌人和玩家共享一个实例，目标自身不再挂载 BuffController。</para>
    /// <para>普通属性 Buff 只参与属性缓存；持续效果才进入对应的调度队列。</para>
    /// </summary>
    public sealed class BuffManner : IFixedTickable, IDisposable, IBuffService
    {
        private const float TimeEpsilon = 0.000001f;
        private const float HealingUpdateIntervalSeconds = 0.1f;
        private static readonly ProfilerMarker FixedTickMarker =
            new("3DBuYu/VContainer/BuffManner.FixedTick");
        private static readonly ProfilerMarker ProcessMarker =
            new("3DBuYu/BuffManner.Process");
        private static readonly ProfilerMarker ProcessHealingQueueMarker =
            new("3DBuYu/BuffManner.ProcessHealingQueue");
        private static readonly ProfilerMarker ProcessExpiryQueueMarker =
            new("3DBuYu/BuffManner.ProcessExpiryQueue");

        private readonly Dictionary<IBuffTarget, TargetState> _targetStates = new();
        private readonly Dictionary<int, ActiveBuffRuntime> _activeBuffs = new();
        private readonly List<ExpiryNode> _expiryQueue = new();
        private readonly List<ActiveBuffRuntime> _healingBuffs = new();
        private readonly List<ActiveBuffRuntime> _damageBuffs = new();

        private float _currentTime;
        private float _healingElapsed;
        private BuffDefinitionSO _damageOverTimeDefinition;
        private readonly IDamageRequestProcessor _damageProcessor;
        private int _nextGeneration;
        private int _nextRuntimeId;
        private bool _disposed;

        /// <summary>创建 Buff 调度器并接入统一伤害提交入口。</summary>
        public BuffManner(IDamageRequestProcessor damageProcessor = null)
        {
            _damageProcessor = damageProcessor ?? new DamageRequestProcessor();
        }

        /// <summary>登记当前可接收 Buff 的目标。</summary>
        /// <param name="target">待登记的目标。</param>
        public void RegisterTarget(IBuffTarget target)
        {
            if (_disposed || !IsReferenceAlive(target))
            {
                return;
            }

            if (_targetStates.TryGetValue(target, out TargetState existingState))
            {
                if (existingState.Target.IsActive && existingState.Target.IsAlive)
                {
                    return;
                }

                // 即使调用方漏掉了一次 OnDisable，也不能把旧租借状态带入新一代。
                UnregisterTarget(target);
            }

            if (!target.IsActive || !target.IsAlive)
            {
                return;
            }

            _nextGeneration++;
            if (_nextGeneration <= 0)
            {
                _nextGeneration = 1;
            }

            _targetStates.Add(
                target,
                new TargetState(
                    target,
                    _nextGeneration,
                    target as IHealable));
        }

        /// <summary>注销目标，并清理该目标上的所有 Buff 和永久移动效果。</summary>
        /// <param name="target">待注销的目标。</param>
        public void UnregisterTarget(IBuffTarget target)
        {
            if (!_targetStates.TryGetValue(target, out TargetState state))
            {
                return;
            }

            ClearState(state, notifyTarget: false);
            _targetStates.Remove(target);
        }

        /// <summary>固定时间推进入口，由 VContainer 的固定 PlayerLoop 调用。</summary>
        public void FixedTick()
        {
            using var profilerScope = FixedTickMarker.Auto();
            Process(Time.fixedDeltaTime);
        }

        /// <summary>
        /// 按给定时间推进所有集中管理的周期 Buff。
        /// 该方法保持公开，便于编辑器测试使用确定的时间步长验证调度规则。
        /// </summary>
        /// <param name="deltaTime">经过的秒数，负值按零处理。</param>
        public void Process(float deltaTime)
        {
            using var profilerScope = ProcessMarker.Auto();
            if (_disposed)
            {
                return;
            }

            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            _currentTime += safeDeltaTime;
            if (_healingBuffs.Count == 0 && _damageBuffs.Count == 0)
            {
                _healingElapsed = 0f;
            }
            else
            {
                _healingElapsed += safeDeltaTime;
                if (_healingElapsed >= HealingUpdateIntervalSeconds)
                {
                    _healingElapsed = 0f;
                    if (_healingBuffs.Count > 0)
                    {
                        ProcessHealingBatch();
                    }
                    if (_damageBuffs.Count > 0)
                    {
                        ProcessDamageBatch();
                    }
                }
            }

            ProcessExpiryQueue();
        }

        /// <summary>向目标应用一个 Buff，并按定义的叠加策略合并状态。</summary>
        /// <param name="target">Buff 目标。</param>
        /// <param name="definition">Buff 静态定义。</param>
        /// <param name="sourceInstanceId">来源实例标识。</param>
        public void ApplyBuff(
            IBuffTarget target,
            BuffDefinitionSO definition,
            int sourceInstanceId)
        {
            if (definition == null || !TryGetUsableState(target, out TargetState state))
            {
                return;
            }

            if (definition.Polarity == BuffPolarity.Harmful &&
                !state.Target.CanReceiveHarmfulEffects)
            {
                return;
            }

            ActiveBuffRuntime existing = null;
            // 空 StackGroupId 的旧定义按 ModifierType 回退；显式组标识可避免不同属性效果误合并。
            if (definition.StackPolicy != BuffStackPolicy.Independent)
            {
                state.MergeableBuffs.TryGetValue(
                    new BuffMergeKey(definition, sourceInstanceId),
                    out existing);
            }

            if (existing == null || !existing.IsActive)
            {
                AddBuff(state, definition, sourceInstanceId);
                return;
            }

            switch (definition.StackPolicy)
            {
                case BuffStackPolicy.AddStack:
                    existing.StackCount = Mathf.Min(
                        Mathf.Max(1, definition.MaximumStackCount),
                        existing.StackCount + 1);
                    RefreshRuntime(
                        state,
                        existing,
                        definition,
                        resetPeriodicStart: false);
                    MarkModifierChanged(state, existing.Definition.ModifierType);
                    break;
                case BuffStackPolicy.Replace:
                    RemoveMergeIndex(state, existing);
                    existing.Definition = definition;
                    existing.StackCount = 1;
                    AddMergeIndex(state, existing);
                    RefreshRuntime(
                        state,
                        existing,
                        definition,
                        resetPeriodicStart: true);
                    MarkModifierChanged(state, definition.ModifierType);
                    break;
                default:
                    RefreshRuntime(
                        state,
                        existing,
                        definition,
                        resetPeriodicStart: false);
                    break;
            }
        }

        /// <summary>施加最多三层、各自计时的火焰持续伤害。</summary>
        public void ApplyDamageOverTime(
            IBuffTarget target,
            float damagePerSecond,
            float durationSeconds,
            int sourceInstanceId)
        {
            if (damagePerSecond <= 0f || durationSeconds <= 0f ||
                !TryGetUsableState(target, out TargetState state) ||
                !state.Target.CanReceiveHarmfulEffects)
            {
                return;
            }

            BuffDefinitionSO definition = GetDamageOverTimeDefinition();
            ActiveBuffRuntime oldestStack = null;
            int stackCount = 0;
            for (int index = 0; index < state.ActiveBuffs.Count; index++)
            {
                ActiveBuffRuntime runtime = state.ActiveBuffs[index];
                if (!runtime.IsActive || runtime.Definition != definition ||
                    runtime.SourceInstanceId != sourceInstanceId)
                {
                    continue;
                }

                stackCount++;
                if (oldestStack == null || runtime.ExpireAt < oldestStack.ExpireAt)
                {
                    oldestStack = runtime;
                }
            }

            if (stackCount < definition.MaximumStackCount)
            {
                ActiveBuffRuntime runtime = AddBuff(
                    state,
                    definition,
                    sourceInstanceId,
                    durationSeconds);
                runtime.DamagePerSecond = damagePerSecond;
                AddDamage(runtime);
                return;
            }

            oldestStack.DamagePerSecond = damagePerSecond;
            oldestStack.RemainingDurationSeconds = durationSeconds;
            oldestStack.ExpireAt = _currentTime + durationSeconds;
            oldestStack.LastEffectTime = _currentTime;
            UpdateExpiry(oldestStack);
        }

        /// <summary>
        /// 仅在目标没有同类活动 Buff 时新增一个 Buff。
        /// 同类判断保持当前行为，只比较 ModifierType，不比较来源。
        /// </summary>
        /// <param name="target">Buff 目标。</param>
        /// <param name="definition">Buff 静态定义。</param>
        /// <param name="sourceInstanceId">来源实例标识。</param>
        /// <returns>本次实际新增时返回 true。</returns>
        public bool TryApplyBuffIfAbsent(
            IBuffTarget target,
            BuffDefinitionSO definition,
            int sourceInstanceId)
        {
            if (definition == null || !TryGetUsableState(target, out TargetState state))
            {
                return false;
            }

            ProcessExpiryQueue();
            if (definition.Polarity == BuffPolarity.Harmful &&
                !state.Target.CanReceiveHarmfulEffects)
            {
                return false;
            }

            for (int index = 0; index < state.ActiveBuffs.Count; index++)
            {
                ActiveBuffRuntime buff = state.ActiveBuffs[index];
                if (buff.IsActive && buff.Definition != null &&
                    buff.Definition.ModifierType == definition.ModifierType)
                {
                    return false;
                }
            }

            AddBuff(state, definition, sourceInstanceId);
            return true;
        }

        /// <summary>查询目标指定属性的集中缓存倍率。</summary>
        /// <param name="target">Buff 目标。</param>
        /// <param name="modifierType">属性类型。</param>
        /// <returns>没有匹配状态时返回一。</returns>
        public float GetModifierMultiplier(
            IBuffTarget target,
            BuffModifierType modifierType)
        {
            if (!TryGetUsableState(target, out TargetState state, registerIfMissing: false))
            {
                return 1f;
            }

            ProcessExpiryQueue();
            if (state.ModifierCacheDirty)
            {
                RebuildModifierCache(state);
            }

            return state.ModifierCache.TryGetValue(modifierType, out float result)
                ? result
                : 1f;
        }

        /// <summary>移除目标上指定来源创建的全部临时 Buff。</summary>
        /// <param name="target">Buff 目标。</param>
        /// <param name="sourceInstanceId">来源实例标识。</param>
        public void RemoveBySourceId(IBuffTarget target, int sourceInstanceId)
        {
            if (!_targetStates.TryGetValue(target, out TargetState state))
            {
                return;
            }

            ProcessExpiryQueue();
            for (int index = state.ActiveBuffs.Count - 1; index >= 0; index--)
            {
                ActiveBuffRuntime buff = state.ActiveBuffs[index];
                if (buff.SourceInstanceId == sourceInstanceId)
                {
                    RemoveRuntime(state, buff);
                }
            }
        }

        /// <summary>清除目标上的全部临时 Buff。</summary>
        /// <param name="target">Buff 目标。</param>
        public void ClearAllBuffs(IBuffTarget target)
        {
            if (_targetStates.TryGetValue(target, out TargetState state))
            {
                ClearTemporaryBuffs(state, notifyTarget: true);
            }
        }

        /// <summary>设置目标指定来源的永久移动速度倍率。</summary>
        /// <param name="target">Buff 目标。</param>
        /// <param name="multiplier">移动速度倍率。</param>
        /// <param name="sourceInstanceId">来源实例标识。</param>
        public void SetPermanentMovementSpeedMultiplier(
            IBuffTarget target,
            float multiplier,
            int sourceInstanceId)
        {
            if (!TryGetUsableState(target, out TargetState state))
            {
                return;
            }

            if (state.PermanentMovementSpeedMultipliers == null)
            {
                state.PermanentMovementSpeedMultipliers = new Dictionary<int, float>();
            }

            state.PermanentMovementSpeedMultipliers[sourceInstanceId] = Mathf.Max(0f, multiplier);
            MarkModifierChanged(state, BuffModifierType.MovementSpeedMultiplier);
        }

        /// <summary>移除目标指定来源的永久移动速度倍率。</summary>
        /// <param name="target">Buff 目标。</param>
        /// <param name="sourceInstanceId">来源实例标识。</param>
        public void ClearPermanentMovementSpeedMultiplier(
            IBuffTarget target,
            int sourceInstanceId)
        {
            if (!_targetStates.TryGetValue(target, out TargetState state) ||
                state.PermanentMovementSpeedMultipliers == null ||
                !state.PermanentMovementSpeedMultipliers.Remove(sourceInstanceId))
            {
                return;
            }

            MarkModifierChanged(state, BuffModifierType.MovementSpeedMultiplier);
        }

        /// <summary>清除目标上的全部永久移动速度倍率。</summary>
        /// <param name="target">Buff 目标。</param>
        public void ClearAllPermanentMovementSpeedMultipliers(IBuffTarget target)
        {
            if (!_targetStates.TryGetValue(target, out TargetState state) ||
                state.PermanentMovementSpeedMultipliers == null ||
                state.PermanentMovementSpeedMultipliers.Count == 0)
            {
                return;
            }

            state.PermanentMovementSpeedMultipliers.Clear();
            MarkModifierChanged(state, BuffModifierType.MovementSpeedMultiplier);
        }

        /// <summary>按来源清除场景内所有目标的永久移动速度倍率。</summary>
        public void ClearPermanentMovementSpeedMultipliersBySourceId(int sourceInstanceId)
        {
            if (_disposed)
            {
                return;
            }

            // 通知回调可能触发目标注销，因此先复制状态列表，避免遍历字典时失效。
            List<TargetState> states = new(_targetStates.Values);
            for (int index = 0; index < states.Count; index++)
            {
                TargetState state = states[index];
                if (state.PermanentMovementSpeedMultipliers == null ||
                    !state.PermanentMovementSpeedMultipliers.Remove(sourceInstanceId))
                {
                    continue;
                }

                MarkModifierChanged(state, BuffModifierType.MovementSpeedMultiplier);
            }
        }

        /// <summary>场景容器销毁时释放所有目标、运行时状态和调度节点。</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            foreach (TargetState state in _targetStates.Values)
            {
                ClearState(state, notifyTarget: false);
            }

            _targetStates.Clear();
            _activeBuffs.Clear();
            _expiryQueue.Clear();
            _healingBuffs.Clear();
            _damageBuffs.Clear();
            if (_damageOverTimeDefinition != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(_damageOverTimeDefinition);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(_damageOverTimeDefinition);
                }
                _damageOverTimeDefinition = null;
            }
            _disposed = true;
        }

        /// <summary>尝试取得一个当前可用的目标状态；写入操作可按需自动登记。</summary>
        private bool TryGetUsableState(
            IBuffTarget target,
            out TargetState state,
            bool registerIfMissing = true)
        {
            state = null;
            if (_disposed || !IsReferenceAlive(target))
            {
                return false;
            }

            if (!_targetStates.TryGetValue(target, out state))
            {
                if (!registerIfMissing)
                {
                    return false;
                }

                RegisterTarget(target);
                if (!_targetStates.TryGetValue(target, out state))
                {
                    return false;
                }
            }

            if (!state.Target.IsActive || !state.Target.IsAlive)
            {
                return false;
            }

            return true;
        }

        /// <summary>把可合并运行时状态写入目标的叠层索引。</summary>
        private static void AddMergeIndex(
            TargetState state,
            ActiveBuffRuntime runtime)
        {
            if (state == null || runtime == null || runtime.Definition == null ||
                runtime.Definition.StackPolicy == BuffStackPolicy.Independent)
            {
                return;
            }

            state.MergeableBuffs[new BuffMergeKey(
                runtime.Definition,
                runtime.SourceInstanceId)] = runtime;
        }

        /// <summary>从目标的叠层索引移除指定运行时状态。</summary>
        private static void RemoveMergeIndex(
            TargetState state,
            ActiveBuffRuntime runtime)
        {
            if (state == null || runtime == null || runtime.Definition == null)
            {
                return;
            }

            BuffMergeKey key = new(runtime.Definition, runtime.SourceInstanceId);
            if (state.MergeableBuffs.TryGetValue(key, out ActiveBuffRuntime indexed) &&
                ReferenceEquals(indexed, runtime))
            {
                state.MergeableBuffs.Remove(key);
            }
        }

        /// <summary>按需创建用于区分持续伤害实例的内部 Buff 定义。</summary>
        private BuffDefinitionSO GetDamageOverTimeDefinition()
        {
            if (_damageOverTimeDefinition != null)
            {
                return _damageOverTimeDefinition;
            }

            _damageOverTimeDefinition = ScriptableObject.CreateInstance<BuffDefinitionSO>();
            _damageOverTimeDefinition.name = "DamageOverTime";
            _damageOverTimeDefinition.ModifierType = BuffModifierType.DamageOverTime;
            _damageOverTimeDefinition.StackGroupId = "ItemBurn";
            _damageOverTimeDefinition.Polarity = BuffPolarity.Harmful;
            _damageOverTimeDefinition.ModifierValue = 1f;
            _damageOverTimeDefinition.StackPolicy = BuffStackPolicy.Independent;
            _damageOverTimeDefinition.MaximumStackCount = 3;
            return _damageOverTimeDefinition;
        }

        /// <summary>新增一个不带重复判断的活动 Buff。</summary>
        private ActiveBuffRuntime AddBuff(
            TargetState state,
            BuffDefinitionSO definition,
            int sourceInstanceId,
            float durationOverride = -1f)
        {
            _nextRuntimeId++;
            if (_nextRuntimeId <= 0)
            {
                _nextRuntimeId = 1;
            }

            float durationSeconds = durationOverride >= 0f
                ? durationOverride
                : definition.DurationSeconds;
            ActiveBuffRuntime runtime = new()
            {
                Definition = definition,
                SourceInstanceId = sourceInstanceId,
                RemainingDurationSeconds = durationSeconds,
                StackCount = 1,
                RuntimeId = _nextRuntimeId,
                TargetGeneration = state.Generation,
                Target = state.Target,
                IsActive = true,
                UsesManagedExpiry = true,
                ExpireAt = durationSeconds > 0f
                    ? _currentTime + durationSeconds
                    : float.PositiveInfinity,
                LastEffectTime = _currentTime,
            };

            state.ActiveBuffs.Add(runtime);
            _activeBuffs.Add(runtime.RuntimeId, runtime);

            if (definition.StackPolicy != BuffStackPolicy.Independent)
            {
                AddMergeIndex(state, runtime);
            }

            if (definition.DurationSeconds > 0f)
            {
                PushExpiry(runtime);
            }

            if (HasHealingEffect(state, runtime))
            {
                AddHealing(runtime);
            }
            if (HasDamageEffect(runtime))
            {
                AddDamage(runtime);
            }

            state.ModifierCacheDirty = true;
            NotifyModifierChanged(state, definition.ModifierType);
            return runtime;
        }

        /// <summary>刷新已有运行时状态的时间和周期调度。</summary>
        private void RefreshRuntime(
            TargetState state,
            ActiveBuffRuntime runtime,
            BuffDefinitionSO durationDefinition,
            bool resetPeriodicStart)
        {
            BuffDefinitionSO definition = runtime.Definition;
            float durationSeconds = durationDefinition != null
                ? durationDefinition.DurationSeconds
                : definition.DurationSeconds;
            runtime.RemainingDurationSeconds = durationSeconds;
            runtime.ExpireAt = durationSeconds > 0f
                ? _currentTime + durationSeconds
                : float.PositiveInfinity;

            if (durationSeconds > 0f)
            {
                if (runtime.ExpiryQueueIndex >= 0)
                {
                    UpdateExpiry(runtime);
                }
                else
                {
                    PushExpiry(runtime);
                }
            }
            else
            {
                RemoveExpiry(runtime);
            }

            if (resetPeriodicStart &&
                (HasHealingEffect(state, runtime) || HasDamageEffect(runtime)))
            {
                runtime.LastEffectTime = _currentTime;
            }

            UpdateHealingSchedule(state, runtime);
            UpdateDamageSchedule(runtime);
        }

        /// <summary>根据当前定义更新持续治疗列表成员资格。</summary>
        private void UpdateHealingSchedule(TargetState state, ActiveBuffRuntime runtime)
        {
            if (!HasHealingEffect(state, runtime))
            {
                RemoveHealing(runtime);
                return;
            }

            if (runtime.HealingListIndex < 0)
            {
                AddHealing(runtime);
            }
        }

        /// <summary>移除一个活动运行时 Buff，并同步索引、队列和属性缓存。</summary>
        private void RemoveRuntime(TargetState state, ActiveBuffRuntime runtime)
        {
            if (runtime == null || !runtime.IsActive)
            {
                return;
            }

            RemoveExpiry(runtime);
            RemoveHealing(runtime);
            RemoveDamage(runtime);
            _activeBuffs.Remove(runtime.RuntimeId);

            RemoveMergeIndex(state, runtime);

            state.ActiveBuffs.Remove(runtime);
            runtime.IsActive = false;
            if (runtime.Definition != null &&
                runtime.UsesManagedExpiry &&
                !float.IsPositiveInfinity(runtime.ExpireAt))
            {
                runtime.RemainingDurationSeconds = 0f;
            }

            if (runtime.Definition != null)
            {
                MarkModifierChanged(state, runtime.Definition.ModifierType);
            }
        }

        /// <summary>清除目标的临时 Buff，不影响独立的永久移动效果。</summary>
        private void ClearTemporaryBuffs(TargetState state, bool notifyTarget)
        {
            List<BuffModifierType> changedModifiers = notifyTarget
                ? new List<BuffModifierType>(state.ActiveBuffs.Count)
                : null;

            for (int index = state.ActiveBuffs.Count - 1; index >= 0; index--)
            {
                ActiveBuffRuntime runtime = state.ActiveBuffs[index];
                if (runtime.Definition != null && changedModifiers != null)
                {
                    changedModifiers.Add(runtime.Definition.ModifierType);
                }

                RemoveExpiry(runtime);
                RemoveHealing(runtime);
                RemoveDamage(runtime);
                _activeBuffs.Remove(runtime.RuntimeId);
                runtime.IsActive = false;
            }

            state.ActiveBuffs.Clear();
            state.MergeableBuffs.Clear();
            state.ModifierCache.Clear();
            state.ModifierCacheDirty = true;

            if (changedModifiers == null || !IsReferenceAlive(state.Target) || !state.Target.IsActive)
            {
                return;
            }

            for (int index = 0; index < changedModifiers.Count; index++)
            {
                state.Target.OnBuffModifiersChanged(changedModifiers[index]);
            }
        }

        /// <summary>清空一个目标状态，包括临时 Buff 和永久移动效果。</summary>
        private void ClearState(TargetState state, bool notifyTarget)
        {
            bool hadPermanentMovementModifiers = state.PermanentMovementSpeedMultipliers != null &&
                state.PermanentMovementSpeedMultipliers.Count > 0;

            ClearTemporaryBuffs(state, notifyTarget: false);
            state.PermanentMovementSpeedMultipliers?.Clear();

            if (!notifyTarget || !IsReferenceAlive(state.Target) || !state.Target.IsActive)
            {
                return;
            }

            if (hadPermanentMovementModifiers)
            {
                state.Target.OnBuffModifiersChanged(BuffModifierType.MovementSpeedMultiplier);
            }
        }

        /// <summary>重新计算一个目标全部属性类别的倍率缓存。</summary>
        private static void RebuildModifierCache(TargetState state)
        {
            state.ModifierCache.Clear();
            for (int index = 0; index < state.ActiveBuffs.Count; index++)
            {
                ActiveBuffRuntime runtime = state.ActiveBuffs[index];
                if (!runtime.IsActive || runtime.Definition == null)
                {
                    continue;
                }

                float value = Mathf.Max(0f, runtime.Definition.ModifierValue);
                float multiplier = 1f;
                for (int stack = 0; stack < Mathf.Max(1, runtime.StackCount); stack++)
                {
                    multiplier *= value;
                }

                if (state.ModifierCache.TryGetValue(
                        runtime.Definition.ModifierType,
                        out float existing))
                {
                    state.ModifierCache[runtime.Definition.ModifierType] = existing * multiplier;
                }
                else
                {
                    state.ModifierCache.Add(runtime.Definition.ModifierType, multiplier);
                }
            }

            if (state.PermanentMovementSpeedMultipliers != null)
            {
                float permanentMultiplier = 1f;
                foreach (float multiplier in state.PermanentMovementSpeedMultipliers.Values)
                {
                    permanentMultiplier *= multiplier;
                }

                if (state.ModifierCache.TryGetValue(
                        BuffModifierType.MovementSpeedMultiplier,
                        out float existing))
                {
                    state.ModifierCache[BuffModifierType.MovementSpeedMultiplier] =
                        existing * permanentMultiplier;
                }
                else if (state.PermanentMovementSpeedMultipliers.Count > 0)
                {
                    state.ModifierCache.Add(
                        BuffModifierType.MovementSpeedMultiplier,
                        permanentMultiplier);
                }
            }

            state.ModifierCacheDirty = false;
        }

        /// <summary>标记属性缓存失效并通知目标同步相关表现或运动状态。</summary>
        private static void MarkModifierChanged(
            TargetState state,
            BuffModifierType modifierType)
        {
            state.ModifierCacheDirty = true;
            NotifyModifierChanged(state, modifierType);
        }

        /// <summary>向有效目标发出一次属性变化通知。</summary>
        private static void NotifyModifierChanged(
            TargetState state,
            BuffModifierType modifierType)
        {
            if (IsReferenceAlive(state.Target) && state.Target.IsActive)
            {
                state.Target.OnBuffModifiersChanged(modifierType);
            }
        }

        /// <summary>判断运行时 Buff 是否需要进入持续治疗队列。</summary>
        private static bool HasHealingEffect(
            TargetState state,
            ActiveBuffRuntime runtime)
        {
            return runtime != null && runtime.IsActive && runtime.Definition != null &&
                runtime.Definition.ModifierType == BuffModifierType.HealingOverTime &&
                runtime.Definition.HealingPerSecond > 0f &&
                IsReferenceAlive(state.Healable);
        }

        /// <summary>判断 Buff 是否包含独立计时的持续伤害。</summary>
        private static bool HasDamageEffect(ActiveBuffRuntime runtime)
        {
            return runtime != null && runtime.IsActive && runtime.Definition != null &&
                runtime.Definition.ModifierType == BuffModifierType.DamageOverTime &&
                runtime.DamagePerSecond > 0f && runtime.Target is IDamageRequestReceiver;
        }

        /// <summary>根据持续伤害状态更新低频调度列表成员资格。</summary>
        private void UpdateDamageSchedule(ActiveBuffRuntime runtime)
        {
            if (!HasDamageEffect(runtime))
            {
                RemoveDamage(runtime);
                return;
            }
            if (runtime.DamageListIndex < 0)
            {
                AddDamage(runtime);
            }
        }

        /// <summary>按低频批次处理当前活动的持续治疗效果。</summary>
        private void ProcessHealingBatch()
        {
            using var profilerScope = ProcessHealingQueueMarker.Auto();
            for (int index = _healingBuffs.Count - 1; index >= 0; index--)
            {
                ActiveBuffRuntime runtime = _healingBuffs[index];
                RemoveHealing(runtime);

                if (!TryGetRuntimeState(runtime, out TargetState state))
                {
                    continue;
                }

                if (!state.Target.IsActive || !state.Target.IsAlive)
                {
                    RemoveRuntime(state, runtime);
                    continue;
                }

                if (!HasHealingEffect(state, runtime))
                {
                    continue;
                }

                float activeEnd = float.IsPositiveInfinity(runtime.ExpireAt)
                    ? _currentTime
                    : Mathf.Min(_currentTime, runtime.ExpireAt);
                float activeTime = Mathf.Max(0f, activeEnd - runtime.LastEffectTime);
                if (activeTime > 0f)
                {
                    float amount = Mathf.Max(0f, runtime.Definition.HealingPerSecond) * activeTime;
                    if (amount > 0f)
                    {
                        state.Healable.Heal(amount);
                    }
                }

                runtime.LastEffectTime = activeEnd;
                if (!runtime.IsActive)
                {
                    continue;
                }

                if (!float.IsPositiveInfinity(runtime.ExpireAt) &&
                    _currentTime >= runtime.ExpireAt - TimeEpsilon)
                {
                    runtime.RemainingDurationSeconds = 0f;
                    RemoveRuntime(state, runtime);
                    continue;
                }

                AddHealing(runtime);
            }
        }

        /// <summary>按活动时间精确积分并提交火焰持续伤害。</summary>
        private void ProcessDamageBatch()
        {
            for (int index = _damageBuffs.Count - 1; index >= 0; index--)
            {
                ActiveBuffRuntime runtime = _damageBuffs[index];
                RemoveDamage(runtime);

                if (!TryGetRuntimeState(runtime, out TargetState state))
                {
                    continue;
                }
                if (!state.Target.IsActive || !state.Target.IsAlive)
                {
                    RemoveRuntime(state, runtime);
                    continue;
                }
                if (!HasDamageEffect(runtime))
                {
                    continue;
                }

                float activeEnd = float.IsPositiveInfinity(runtime.ExpireAt)
                    ? _currentTime
                    : Mathf.Min(_currentTime, runtime.ExpireAt);
                float activeTime = Mathf.Max(0f, activeEnd - runtime.LastEffectTime);
                float damage = runtime.DamagePerSecond * activeTime;
                if (damage > 0f && state.Target is IDamageable damageable)
                {
                    DamageRequest request = new()
                    {
                        SourceInstanceId = runtime.SourceInstanceId,
                        DamageAmount = damage,
                        DamageType = DamageType.Fire,
                        HitPosition = damageable.Position,
                        HitNormal = Vector3.up
                    };
                    _damageProcessor.TryApplyDamage(damageable, request, out _);
                }

                runtime.LastEffectTime = activeEnd;
                if (!runtime.IsActive)
                {
                    continue;
                }
                if (!float.IsPositiveInfinity(runtime.ExpireAt) &&
                    _currentTime >= runtime.ExpireAt - TimeEpsilon)
                {
                    runtime.RemainingDurationSeconds = 0f;
                    RemoveRuntime(state, runtime);
                    continue;
                }

                AddDamage(runtime);
            }
        }

        /// <summary>处理已经到期的 Buff，普通属性 Buff 不会被逐目标扫描。</summary>
        private void ProcessExpiryQueue()
        {
            using var profilerScope = ProcessExpiryQueueMarker.Auto();
            while (TryPeekExpiry(out ExpiryNode node) &&
                node.DueAt <= _currentTime + TimeEpsilon)
            {
                ActiveBuffRuntime runtime = node.Runtime;
                RemoveExpiry(runtime);

                if (!TryGetRuntimeState(runtime, out TargetState state))
                {
                    continue;
                }

                if (runtime.ExpireAt > _currentTime + TimeEpsilon)
                {
                    PushExpiry(runtime);
                    continue;
                }

                runtime.RemainingDurationSeconds = 0f;
                RemoveRuntime(state, runtime);
            }
        }

        /// <summary>取得运行时 Buff 所属的有效目标状态。</summary>
        private bool TryGetRuntimeState(
            ActiveBuffRuntime runtime,
            out TargetState state)
        {
            state = null;
            if (runtime == null || !runtime.IsActive ||
                !_activeBuffs.TryGetValue(runtime.RuntimeId, out ActiveBuffRuntime indexed) ||
                !ReferenceEquals(indexed, runtime) ||
                !IsReferenceAlive(runtime.Target) ||
                !_targetStates.TryGetValue(runtime.Target, out state) ||
                state.Generation != runtime.TargetGeneration)
            {
                return false;
            }

            return true;
        }

        /// <summary>取得持续治疗的固定调度间隔。</summary>
        #region Expiry min-heap

        private void PushExpiry(ActiveBuffRuntime runtime)
        {
            if (runtime == null || runtime.ExpiryQueueIndex >= 0 ||
                float.IsPositiveInfinity(runtime.ExpireAt))
            {
                return;
            }

            runtime.ExpiryQueueIndex = _expiryQueue.Count;
            _expiryQueue.Add(new ExpiryNode(runtime, runtime.ExpireAt));
            SiftUpExpiry(runtime.ExpiryQueueIndex);
        }

        private void UpdateExpiry(ActiveBuffRuntime runtime)
        {
            if (runtime == null)
            {
                return;
            }

            if (runtime.ExpiryQueueIndex < 0)
            {
                PushExpiry(runtime);
                return;
            }

            int index = runtime.ExpiryQueueIndex;
            if (index >= _expiryQueue.Count ||
                !ReferenceEquals(_expiryQueue[index].Runtime, runtime))
            {
                runtime.ExpiryQueueIndex = -1;
                PushExpiry(runtime);
                return;
            }

            ExpiryNode node = _expiryQueue[index];
            node.DueAt = runtime.ExpireAt;
            _expiryQueue[index] = node;
            SiftUpExpiry(index);
            SiftDownExpiry(runtime.ExpiryQueueIndex);
        }

        private void RemoveExpiry(ActiveBuffRuntime runtime)
        {
            if (runtime == null || runtime.ExpiryQueueIndex < 0)
            {
                return;
            }

            int index = runtime.ExpiryQueueIndex;
            runtime.ExpiryQueueIndex = -1;
            if (index >= _expiryQueue.Count ||
                !ReferenceEquals(_expiryQueue[index].Runtime, runtime))
            {
                return;
            }

            int lastIndex = _expiryQueue.Count - 1;
            if (index == lastIndex)
            {
                _expiryQueue.RemoveAt(lastIndex);
                return;
            }

            ExpiryNode replacement = _expiryQueue[lastIndex];
            _expiryQueue[index] = replacement;
            replacement.Runtime.ExpiryQueueIndex = index;
            _expiryQueue.RemoveAt(lastIndex);
            SiftUpExpiry(index);
            SiftDownExpiry(replacement.Runtime.ExpiryQueueIndex);
        }

        private bool TryPeekExpiry(out ExpiryNode node)
        {
            if (_expiryQueue.Count == 0)
            {
                node = default;
                return false;
            }

            node = _expiryQueue[0];
            return true;
        }

        private void SiftUpExpiry(int index)
        {
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (!IsEarlier(_expiryQueue[index], _expiryQueue[parent]))
                {
                    break;
                }

                SwapExpiry(index, parent);
                index = parent;
            }
        }

        private void SiftDownExpiry(int index)
        {
            while (true)
            {
                int left = index * 2 + 1;
                if (left >= _expiryQueue.Count)
                {
                    return;
                }

                int right = left + 1;
                int smallest = left;
                if (right < _expiryQueue.Count &&
                    IsEarlier(_expiryQueue[right], _expiryQueue[left]))
                {
                    smallest = right;
                }

                if (!IsEarlier(_expiryQueue[smallest], _expiryQueue[index]))
                {
                    return;
                }

                SwapExpiry(index, smallest);
                index = smallest;
            }
        }

        private void SwapExpiry(int first, int second)
        {
            ExpiryNode value = _expiryQueue[first];
            _expiryQueue[first] = _expiryQueue[second];
            _expiryQueue[second] = value;
            _expiryQueue[first].Runtime.ExpiryQueueIndex = first;
            _expiryQueue[second].Runtime.ExpiryQueueIndex = second;
        }

        private static bool IsEarlier(ExpiryNode left, ExpiryNode right)
        {
            if (left.DueAt < right.DueAt)
            {
                return true;
            }

            return left.DueAt == right.DueAt &&
                left.Runtime.RuntimeId < right.Runtime.RuntimeId;
        }

        #endregion

        #region Healing schedule

        private void AddHealing(ActiveBuffRuntime runtime)
        {
            if (runtime == null || runtime.HealingListIndex >= 0)
            {
                return;
            }

            runtime.HealingListIndex = _healingBuffs.Count;
            _healingBuffs.Add(runtime);
        }

        private void RemoveHealing(ActiveBuffRuntime runtime)
        {
            if (runtime == null || runtime.HealingListIndex < 0)
            {
                return;
            }

            int index = runtime.HealingListIndex;
            runtime.HealingListIndex = -1;
            if (index >= _healingBuffs.Count ||
                !ReferenceEquals(_healingBuffs[index], runtime))
            {
                return;
            }

            int lastIndex = _healingBuffs.Count - 1;
            if (index == lastIndex)
            {
                _healingBuffs.RemoveAt(lastIndex);
                return;
            }

            ActiveBuffRuntime replacement = _healingBuffs[lastIndex];
            _healingBuffs[index] = replacement;
            replacement.HealingListIndex = index;
            _healingBuffs.RemoveAt(lastIndex);
        }

        /// <summary>将持续伤害加入集中调度列表。</summary>
        private void AddDamage(ActiveBuffRuntime runtime)
        {
            if (runtime == null || runtime.DamageListIndex >= 0)
            {
                return;
            }

            runtime.DamageListIndex = _damageBuffs.Count;
            _damageBuffs.Add(runtime);
        }

        /// <summary>将持续伤害从集中调度列表移除。</summary>
        private void RemoveDamage(ActiveBuffRuntime runtime)
        {
            if (runtime == null || runtime.DamageListIndex < 0)
            {
                return;
            }

            int index = runtime.DamageListIndex;
            runtime.DamageListIndex = -1;
            if (index >= _damageBuffs.Count ||
                !ReferenceEquals(_damageBuffs[index], runtime))
            {
                return;
            }

            int lastIndex = _damageBuffs.Count - 1;
            if (index == lastIndex)
            {
                _damageBuffs.RemoveAt(lastIndex);
                return;
            }

            ActiveBuffRuntime replacement = _damageBuffs[lastIndex];
            _damageBuffs[index] = replacement;
            replacement.DamageListIndex = index;
            _damageBuffs.RemoveAt(lastIndex);
        }

        #endregion

        /// <summary>判断普通引用或 Unity 对象引用是否仍然有效。</summary>
        private static bool IsReferenceAlive(object value)
        {
            if (value == null)
            {
                return false;
            }

            return !(value is UnityEngine.Object unityObject) || unityObject != null;
        }

        private sealed class TargetState
        {
            public readonly IBuffTarget Target;
            public readonly IHealable Healable;
            public readonly int Generation;
            public readonly List<ActiveBuffRuntime> ActiveBuffs = new();
            public readonly Dictionary<BuffMergeKey, ActiveBuffRuntime> MergeableBuffs = new();
            public readonly Dictionary<BuffModifierType, float> ModifierCache = new();
            public Dictionary<int, float> PermanentMovementSpeedMultipliers;
            public bool ModifierCacheDirty = true;

            public TargetState(
                IBuffTarget target,
                int generation,
                IHealable healable)
            {
                Target = target;
                Generation = generation;
                Healable = healable;
            }
        }

        /// <summary>同来源、同叠层组的状态索引；旧资产为空组时按属性类别兼容。</summary>
        private readonly struct BuffMergeKey : IEquatable<BuffMergeKey>
        {
            private readonly BuffModifierType _modifierType;
            private readonly string _stackGroupId;
            private readonly int _sourceInstanceId;

            public BuffMergeKey(
                BuffDefinitionSO definition,
                int sourceInstanceId)
            {
                _modifierType = definition != null
                    ? definition.ModifierType
                    : default;
                _stackGroupId = definition != null &&
                    !string.IsNullOrWhiteSpace(definition.StackGroupId)
                    ? definition.StackGroupId
                    : null;
                _sourceInstanceId = sourceInstanceId;
            }

            public bool Equals(BuffMergeKey other)
            {
                if (_sourceInstanceId != other._sourceInstanceId)
                {
                    return false;
                }

                if (_stackGroupId != null || other._stackGroupId != null)
                {
                    return string.Equals(
                        _stackGroupId,
                        other._stackGroupId,
                        StringComparison.Ordinal);
                }

                return _modifierType == other._modifierType;
            }

            public override bool Equals(object obj)
            {
                return obj is BuffMergeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int identityHash = _stackGroupId != null
                        ? StringComparer.Ordinal.GetHashCode(_stackGroupId)
                        : (int)_modifierType;
                    return (identityHash * 397) ^ _sourceInstanceId;
                }
            }
        }

        private struct ExpiryNode
        {
            public ActiveBuffRuntime Runtime;
            public float DueAt;

            public ExpiryNode(ActiveBuffRuntime runtime, float dueAt)
            {
                Runtime = runtime;
                DueAt = dueAt;
            }
        }

    }
}
