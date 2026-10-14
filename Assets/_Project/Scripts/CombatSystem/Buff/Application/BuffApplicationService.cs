using System.Collections.Generic;
using Interfaces;
using Services;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>协调目标状态、Buff 实例变更和集中效果调度。</summary>
    internal sealed class BuffApplicationService
    {
        private readonly BuffStateStore _states;
        private readonly BuffInstanceActions _instances;
        private readonly BuffEffectScheduler _scheduler;
        private readonly BuffVisualEffectService _visualEffects;
        private bool _disposed;

        /// <summary>创建 Buff 状态服务并绑定伤害与池化视效入口。</summary>
        internal BuffApplicationService(
            IDamageRequestProcessor damageProcessor,
            IPooledEffectService pooledEffectService)
        {
            _states = new BuffStateStore();
            _instances = new BuffInstanceActions(_states);
            _visualEffects = new BuffVisualEffectService(pooledEffectService);
            _scheduler = new BuffEffectScheduler(
                damageProcessor,
                _states.FindInstanceState,
                RemoveScheduledInstance);
        }

        /// <summary>登记当前可接收 Buff 的目标。</summary>
        internal void RegisterTarget(IBuffTarget target)
        {
            if (_disposed || !BuffStateStore.IsReferenceAlive(target))
            {
                return;
            }

            if (_states.TryGetTargetState(target, out BuffTargetState existingState))
            {
                if (existingState.Target.IsActive && existingState.Target.IsAlive)
                {
                    return;
                }

                UnregisterTarget(target);
            }

            if (!target.IsActive || !target.IsAlive)
            {
                return;
            }

            _states.RegisterTarget(target, target as IHealable);
        }

        /// <summary>注销目标并清理该目标上的临时和永久效果。</summary>
        internal void UnregisterTarget(IBuffTarget target)
        {
            if (!_states.TryGetTargetState(target, out BuffTargetState state))
            {
                return;
            }

            ClearState(state, notifyTarget: false);
            _states.RemoveTarget(target);
        }

        /// <summary>推进所有集中管理的周期 Buff 和到期状态。</summary>
        internal void Process(float deltaTime)
        {
            if (!_disposed)
            {
                _scheduler.Process(deltaTime);
            }
        }

        /// <summary>向目标应用一个 Buff，并按定义的叠加策略合并状态。</summary>
        internal void ApplyBuff(
            IBuffTarget target,
            BuffDefinitionSO definition,
            int sourceInstanceId)
        {
            if (definition == null || !TryGetUsableState(target, out BuffTargetState state))
            {
                return;
            }

            if (definition.Polarity == BuffPolarity.Harmful &&
                !state.Target.CanReceiveHarmfulEffects)
            {
                return;
            }

            BuffInstance existing = null;
            // 合并键由来源、ModifierType 和规范化后的 StackGroupId 共同决定。
            if (definition.StackPolicy != BuffStackPolicy.Independent)
            {
                state.TryGetMergeable(definition, sourceInstanceId, out existing);
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
                    RefreshInstance(
                        state,
                        existing,
                        definition,
                        BuffRefreshMode.PreservePeriodicStart);
                    state.MarkModifierChanged(existing.ModifierType);
                    break;
                case BuffStackPolicy.Replace:
                    state.RemoveMergeIndex(existing);
                    existing.Definition = definition;
                    existing.StackCount = 1;
                    state.AddMergeIndex(existing);
                    RefreshInstance(
                        state,
                        existing,
                        definition,
                        BuffRefreshMode.ResetPeriodicStart);
                    state.MarkModifierChanged(definition.ModifierType);
                    break;
                default:
                    RefreshInstance(
                        state,
                        existing,
                        definition,
                        BuffRefreshMode.PreservePeriodicStart);
                    break;
            }
        }

        /// <summary>施加最多三层、各自计时的火焰持续伤害。</summary>
        internal void ApplyDamageOverTime(
            IBuffTarget target,
            float damagePerSecond,
            float durationSeconds,
            int sourceInstanceId)
        {
            if (target is not IDamageRequestReceiver ||
                damagePerSecond <= 0f || durationSeconds <= 0f ||
                !TryGetUsableState(target, out BuffTargetState state) ||
                !state.Target.CanReceiveHarmfulEffects)
            {
                return;
            }

            BuffInstance oldestInstance = null;
            int stackCount = 0;
            for (int index = 0; index < state.ActiveInstances.Count; index++)
            {
                BuffInstance instance = state.ActiveInstances[index];
                if (!instance.IsActive || !instance.IsDamageOverTime ||
                    instance.SourceInstanceId != sourceInstanceId)
                {
                    continue;
                }

                stackCount++;
                if (oldestInstance == null || instance.ExpireAt < oldestInstance.ExpireAt)
                {
                    oldestInstance = instance;
                }
            }

            if (stackCount < BuffInstance.DamageOverTimeStackLimit)
            {
                BuffInstance instance = _instances.CreateDamageOverTime(
                    state,
                    sourceInstanceId,
                    damagePerSecond,
                    durationSeconds,
                    _scheduler.CurrentTime);
                _scheduler.Add(state, instance);
                return;
            }

            oldestInstance.DamagePerSecond = damagePerSecond;
            oldestInstance.RemainingDurationSeconds = durationSeconds;
            oldestInstance.ExpireAt = _scheduler.CurrentTime + durationSeconds;
            oldestInstance.LastEffectTime = _scheduler.CurrentTime;
            _scheduler.UpdateExpiry(oldestInstance);
        }

        /// <summary>仅在目标没有同类活动 Buff 时新增一个 Buff。</summary>
        internal bool TryApplyBuffIfAbsent(
            IBuffTarget target,
            BuffDefinitionSO definition,
            int sourceInstanceId)
        {
            if (definition == null || !TryGetUsableState(target, out BuffTargetState state))
            {
                return false;
            }

            _scheduler.ProcessExpiryQueue();
            if (definition.Polarity == BuffPolarity.Harmful &&
                !state.Target.CanReceiveHarmfulEffects)
            {
                return false;
            }

            if (state.HasModifier(definition.ModifierType))
            {
                return false;
            }

            AddBuff(state, definition, sourceInstanceId);
            return true;
        }

        /// <summary>查询目标指定属性的集中缓存倍率。</summary>
        internal float GetModifierMultiplier(
            IBuffTarget target,
            BuffModifierType modifierType)
        {
            if (!TryGetUsableState(target, out BuffTargetState state, registerIfMissing: false))
            {
                return 1f;
            }

            _scheduler.ProcessExpiryQueue();
            return state.GetModifierMultiplier(modifierType);
        }

        /// <summary>移除目标上指定来源创建的全部临时 Buff。</summary>
        internal void RemoveBySourceId(IBuffTarget target, int sourceInstanceId)
        {
            if (!_states.TryGetTargetState(target, out BuffTargetState state))
            {
                return;
            }

            _scheduler.ProcessExpiryQueue();
            for (int index = state.ActiveInstances.Count - 1; index >= 0; index--)
            {
                BuffInstance instance = state.ActiveInstances[index];
                if (instance.SourceInstanceId == sourceInstanceId)
                {
                    RemoveInstance(state, instance);
                }
            }
        }

        /// <summary>清除目标上的全部临时 Buff。</summary>
        internal void ClearAllBuffs(IBuffTarget target)
        {
            if (_states.TryGetTargetState(target, out BuffTargetState state))
            {
                ClearTemporaryBuffs(state, notifyTarget: true);
            }
        }

        /// <summary>设置目标指定来源的永久移动速度倍率。</summary>
        internal void SetPermanentMovementSpeedMultiplier(
            IBuffTarget target,
            float multiplier,
            int sourceInstanceId)
        {
            if (!TryGetUsableState(target, out BuffTargetState state))
            {
                return;
            }

            state.SetPermanentMovementMultiplier(sourceInstanceId, multiplier);
            state.MarkModifierChanged(BuffModifierType.MovementSpeedMultiplier);
        }

        /// <summary>移除目标指定来源的永久移动速度倍率。</summary>
        internal void ClearPermanentMovementSpeedMultiplier(
            IBuffTarget target,
            int sourceInstanceId)
        {
            if (!_states.TryGetTargetState(target, out BuffTargetState state) ||
                !state.RemovePermanentMovementMultiplier(sourceInstanceId))
            {
                return;
            }

            state.MarkModifierChanged(BuffModifierType.MovementSpeedMultiplier);
        }

        /// <summary>清除目标上的全部永久移动速度倍率。</summary>
        internal void ClearAllPermanentMovementSpeedMultipliers(IBuffTarget target)
        {
            if (!_states.TryGetTargetState(target, out BuffTargetState state) ||
                !state.ClearPermanentMovementMultipliers())
            {
                return;
            }

            state.MarkModifierChanged(BuffModifierType.MovementSpeedMultiplier);
        }

        /// <summary>按来源清除场景内所有目标的永久移动速度倍率。</summary>
        internal void ClearPermanentMovementSpeedMultipliersBySourceId(int sourceInstanceId)
        {
            if (_disposed)
            {
                return;
            }

            List<BuffTargetState> states = new(_states.Targets);
            for (int index = 0; index < states.Count; index++)
            {
                BuffTargetState state = states[index];
                if (state.RemovePermanentMovementMultiplier(sourceInstanceId))
                {
                    state.MarkModifierChanged(BuffModifierType.MovementSpeedMultiplier);
                }
            }
        }

        /// <summary>释放全部目标状态和调度队列节点。</summary>
        internal void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            foreach (BuffTargetState state in _states.Targets)
            {
                ClearState(state, notifyTarget: false);
            }

            _states.Clear();
            _scheduler.Clear();
            _visualEffects.Clear();
            _disposed = true;
        }

        private bool TryGetUsableState(
            IBuffTarget target,
            out BuffTargetState state,
            bool registerIfMissing = true)
        {
            state = null;
            if (_disposed || !BuffStateStore.IsReferenceAlive(target))
            {
                return false;
            }

            if (!_states.TryGetTargetState(target, out state))
            {
                if (!registerIfMissing)
                {
                    return false;
                }

                RegisterTarget(target);
                if (!_states.TryGetTargetState(target, out state))
                {
                    return false;
                }
            }

            return state.Target.IsActive && state.Target.IsAlive;
        }

        private BuffInstance AddBuff(
            BuffTargetState state,
            BuffDefinitionSO definition,
            int sourceInstanceId)
        {
            BuffInstance instance = _instances.Create(
                state,
                definition,
                sourceInstanceId,
                _scheduler.CurrentTime);
            _scheduler.Add(state, instance);
            _visualEffects.Start(instance, state.Target);
            return instance;
        }

        private void RefreshInstance(
            BuffTargetState state,
            BuffInstance instance,
            BuffDefinitionSO durationDefinition,
            BuffRefreshMode refreshMode)
        {
            _instances.Refresh(instance, durationDefinition, _scheduler.CurrentTime);
            _scheduler.Refresh(state, instance, refreshMode);
            _visualEffects.Refresh(instance, state.Target);
        }

        private void RemoveInstance(BuffTargetState state, BuffInstance instance)
        {
            if (instance == null || !instance.IsActive)
            {
                return;
            }

            _scheduler.Remove(instance);
            RemoveScheduledInstance(state, instance);
        }

        private void RemoveScheduledInstance(BuffTargetState state, BuffInstance instance)
        {
            _visualEffects.Stop(instance);
            _instances.Remove(state, instance);
        }

        private void ClearTemporaryBuffs(BuffTargetState state, bool notifyTarget)
        {
            List<BuffModifierType> changedModifiers = notifyTarget
                ? new List<BuffModifierType>(state.ActiveInstances.Count)
                : null;

            for (int index = state.ActiveInstances.Count - 1; index >= 0; index--)
            {
                BuffInstance instance = state.ActiveInstances[index];
                if (changedModifiers != null)
                {
                    changedModifiers.Add(instance.ModifierType);
                }

                _visualEffects.Stop(instance);
                _scheduler.Remove(instance);
                _instances.RemoveWithoutNotification(state, instance);
            }

            state.ClearTemporaryInstances();
            if (changedModifiers == null ||
                !BuffStateStore.IsReferenceAlive(state.Target) || !state.Target.IsActive)
            {
                return;
            }

            for (int index = 0; index < changedModifiers.Count; index++)
            {
                state.NotifyModifierChanged(changedModifiers[index]);
            }
        }

        private void ClearState(BuffTargetState state, bool notifyTarget)
        {
            bool hadPermanentMovementMultipliers = state.HasPermanentMovementMultipliers;
            ClearTemporaryBuffs(state, notifyTarget: false);
            state.ClearPermanentMovementMultipliers();

            if (!notifyTarget || !BuffStateStore.IsReferenceAlive(state.Target) ||
                !state.Target.IsActive)
            {
                return;
            }

            if (hadPermanentMovementMultipliers)
            {
                state.NotifyModifierChanged(BuffModifierType.MovementSpeedMultiplier);
            }
        }
    }
}
