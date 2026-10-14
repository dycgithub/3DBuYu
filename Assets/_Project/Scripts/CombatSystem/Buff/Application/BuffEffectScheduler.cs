using System;
using System.Collections.Generic;
using Interfaces;
using Unity.Profiling;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>集中推进 Buff 到期、持续治疗和持续伤害。</summary>
    internal sealed class BuffEffectScheduler
    {
        private const float TimeEpsilon = 0.000001f;
        private const float HealingUpdateIntervalSeconds = 0.1f;
        private static readonly ProfilerMarker ProcessHealingQueueMarker =
            new("3DBuYu/BuffService.ProcessHealingQueue");
        private static readonly ProfilerMarker ProcessExpiryQueueMarker =
            new("3DBuYu/BuffService.ProcessExpiryQueue");

        private readonly BuffExpiryQueue _expiryQueue = new();
        private readonly List<BuffInstance> _healingInstances = new();
        private readonly List<BuffInstance> _damageInstances = new();
        private readonly IDamageRequestProcessor _damageProcessor;
        private readonly Func<BuffInstance, BuffTargetState> _findTargetState;
        private readonly Action<BuffTargetState, BuffInstance> _removeInstance;

        private float _currentTime;
        private float _healingElapsed;

        internal float CurrentTime => _currentTime;

        /// <summary>绑定伤害入口和实例所属状态解析逻辑。</summary>
        internal BuffEffectScheduler(
            IDamageRequestProcessor damageProcessor,
            Func<BuffInstance, BuffTargetState> findTargetState,
            Action<BuffTargetState, BuffInstance> removeInstance)
        {
            _damageProcessor = damageProcessor;
            _findTargetState = findTargetState;
            _removeInstance = removeInstance;
        }

        /// <summary>按给定步长推进当前时间、周期效果和到期实例。</summary>
        internal void Process(float deltaTime)
        {
            float safeDeltaTime = Mathf.Max(0f, deltaTime);
            _currentTime += safeDeltaTime;
            if (_healingInstances.Count == 0 && _damageInstances.Count == 0)
            {
                _healingElapsed = 0f;
            }
            else
            {
                _healingElapsed += safeDeltaTime;
                // 避免分段步长累加在十赫兹边界下方产生浮点舍入误差。
                if (_healingElapsed >= HealingUpdateIntervalSeconds - TimeEpsilon)
                {
                    _healingElapsed = 0f;
                    if (_healingInstances.Count > 0)
                    {
                        ProcessHealingBatch();
                    }

                    if (_damageInstances.Count > 0)
                    {
                        ProcessDamageBatch();
                    }
                }
            }

            ProcessExpiryQueue();
        }

        /// <summary>把新实例加入到期和对应周期效果队列。</summary>
        internal void Add(BuffTargetState state, BuffInstance instance)
        {
            // 灼烧层由周期批次积分到 ExpireAt 后移除；其余有限时长实例进入到期堆。
            if (!instance.IsDamageOverTime && !float.IsPositiveInfinity(instance.ExpireAt))
            {
                _expiryQueue.Add(instance);
            }

            UpdatePeriodicEffects(state, instance);
        }

        /// <summary>刷新实例的到期节点和周期效果队列成员资格。</summary>
        internal void Refresh(
            BuffTargetState state,
            BuffInstance instance,
            BuffRefreshMode refreshMode)
        {
            if (!float.IsPositiveInfinity(instance.ExpireAt))
            {
                _expiryQueue.Update(instance);
            }
            else
            {
                _expiryQueue.Remove(instance);
            }

            if (refreshMode == BuffRefreshMode.ResetPeriodicStart &&
                (HasHealingEffect(state, instance) || HasDamageEffect(instance)))
            {
                instance.LastEffectTime = _currentTime;
            }

            UpdatePeriodicEffects(state, instance);
        }

        /// <summary>从所有调度队列中移除实例。</summary>
        internal void Remove(BuffInstance instance)
        {
            _expiryQueue.Remove(instance);
            RemoveHealing(instance);
            RemoveDamage(instance);
        }

        /// <summary>更新实例持续效果队列的成员资格。</summary>
        internal void UpdatePeriodicEffects(BuffTargetState state, BuffInstance instance)
        {
            UpdateHealingSchedule(state, instance);

            if (HasDamageEffect(instance))
            {
                AddDamage(instance);
            }
            else
            {
                RemoveDamage(instance);
            }
        }

        /// <summary>更新已有的到期堆节点；缺少节点时重新插入。</summary>
        internal void UpdateExpiry(BuffInstance instance)
        {
            _expiryQueue.Update(instance);
        }

        /// <summary>单独处理已到期实例，供同步查询先清理过期状态。</summary>
        internal void ProcessExpiryQueue()
        {
            using var profilerScope = ProcessExpiryQueueMarker.Auto();
            while (_expiryQueue.TryPeek(out BuffExpiryNode node) &&
                node.DueAt <= _currentTime + TimeEpsilon)
            {
                BuffInstance instance = node.Instance;
                _expiryQueue.Remove(instance);

                BuffTargetState state = _findTargetState(instance);
                if (state == null)
                {
                    continue;
                }

                if (instance.ExpireAt > _currentTime + TimeEpsilon)
                {
                    _expiryQueue.Add(instance);
                    continue;
                }

                if (state.Target.IsActive && state.Target.IsAlive &&
                    HasHealingEffect(state, instance))
                {
                    ApplyHealingThrough(state, instance, instance.ExpireAt);
                }

                instance.RemainingDurationSeconds = 0f;
                RemoveInstance(state, instance);
            }
        }

        /// <summary>释放调度队列中的所有节点。</summary>
        internal void Clear()
        {
            _expiryQueue.Clear();
            _healingInstances.Clear();
            _damageInstances.Clear();
            _healingElapsed = 0f;
        }

        private void ProcessHealingBatch()
        {
            using var profilerScope = ProcessHealingQueueMarker.Auto();
            for (int index = _healingInstances.Count - 1; index >= 0; index--)
            {
                BuffInstance instance = _healingInstances[index];
                RemoveHealing(instance);

                BuffTargetState state = _findTargetState(instance);
                if (state == null)
                {
                    continue;
                }

                if (!state.Target.IsActive || !state.Target.IsAlive)
                {
                    RemoveInstance(state, instance);
                    continue;
                }

                if (!HasHealingEffect(state, instance))
                {
                    continue;
                }

                float activeEnd = float.IsPositiveInfinity(instance.ExpireAt)
                    ? _currentTime
                    : Mathf.Min(_currentTime, instance.ExpireAt);
                ApplyHealingThrough(state, instance, activeEnd);
                if (!instance.IsActive)
                {
                    continue;
                }

                if (!float.IsPositiveInfinity(instance.ExpireAt) &&
                    _currentTime >= instance.ExpireAt - TimeEpsilon)
                {
                    instance.RemainingDurationSeconds = 0f;
                    RemoveInstance(state, instance);
                    continue;
                }

                AddHealing(instance);
            }
        }

        private static void ApplyHealingThrough(
            BuffTargetState state,
            BuffInstance instance,
            float activeEnd)
        {
            float activeTime = Mathf.Max(0f, activeEnd - instance.LastEffectTime);
            float amount = Mathf.Max(0f, instance.HealingPerSecond) * activeTime;
            if (amount > 0f)
            {
                state.Healable.Heal(amount);
            }

            instance.LastEffectTime = activeEnd;
        }

        private void ProcessDamageBatch()
        {
            for (int index = _damageInstances.Count - 1; index >= 0; index--)
            {
                BuffInstance instance = _damageInstances[index];
                RemoveDamage(instance);

                BuffTargetState state = _findTargetState(instance);
                if (state == null)
                {
                    continue;
                }

                if (!state.Target.IsActive || !state.Target.IsAlive)
                {
                    RemoveInstance(state, instance);
                    continue;
                }

                if (!HasDamageEffect(instance))
                {
                    continue;
                }

                float activeEnd = float.IsPositiveInfinity(instance.ExpireAt)
                    ? _currentTime
                    : Mathf.Min(_currentTime, instance.ExpireAt);
                float activeTime = Mathf.Max(0f, activeEnd - instance.LastEffectTime);
                float damage = instance.DamagePerSecond * activeTime;
                if (damage > 0f)
                {
                    SubmitDamage(state, instance, damage);
                }

                instance.LastEffectTime = activeEnd;
                if (!instance.IsActive)
                {
                    continue;
                }

                if (!float.IsPositiveInfinity(instance.ExpireAt) &&
                    _currentTime >= instance.ExpireAt - TimeEpsilon)
                {
                    instance.RemainingDurationSeconds = 0f;
                    RemoveInstance(state, instance);
                    continue;
                }

                AddDamage(instance);
            }
        }

        private void RemoveInstance(BuffTargetState state, BuffInstance instance)
        {
            Remove(instance);
            _removeInstance(state, instance);
        }

        /// <summary>通过统一处理器提交完整目标请求，并支持无世界坐标的接收器。</summary>
        private void SubmitDamage(
            BuffTargetState state,
            BuffInstance instance,
            float damage)
        {
            IDamageable damageable = state.Target as IDamageable;
            DamageRequest request = new()
            {
                SourceInstanceId = instance.SourceInstanceId,
                DamageAmount = damage,
                DamageType = DamageType.Fire,
                HitPosition = damageable != null ? damageable.Position : Vector3.zero,
                HitNormal = Vector3.up
            };

            if (damageable != null && _damageProcessor != null)
            {
                _damageProcessor.TryApplyDamage(damageable, request, out _);
                return;
            }

            // 轻量接收器不要求实现提供世界位置的 IDamageable 契约。
            if (state.Target is IDamageRequestReceiver receiver)
            {
                receiver.ReceiveDamageRequest(request);
            }
        }

        private static bool HasHealingEffect(BuffTargetState state, BuffInstance instance)
        {
            return instance != null && instance.IsActive &&
                instance.ModifierType == BuffModifierType.HealingOverTime &&
                instance.HealingPerSecond > 0f &&
                BuffStateStore.IsReferenceAlive(state.Healable);
        }

        private static bool HasDamageEffect(BuffInstance instance)
        {
            return instance != null && instance.IsActive && instance.IsDamageOverTime &&
                instance.DamagePerSecond > 0f && instance.Target is IDamageRequestReceiver;
        }

        private void UpdateHealingSchedule(BuffTargetState state, BuffInstance instance)
        {
            if (HasHealingEffect(state, instance))
            {
                AddHealing(instance);
            }
            else
            {
                RemoveHealing(instance);
            }
        }

        private void AddHealing(BuffInstance instance)
        {
            if (instance == null || instance.HealingListIndex >= 0)
            {
                return;
            }

            instance.HealingListIndex = _healingInstances.Count;
            _healingInstances.Add(instance);
        }

        private void RemoveHealing(BuffInstance instance)
        {
            if (instance == null || instance.HealingListIndex < 0)
            {
                return;
            }

            int index = instance.HealingListIndex;
            instance.HealingListIndex = -1;
            if (index >= _healingInstances.Count ||
                !ReferenceEquals(_healingInstances[index], instance))
            {
                return;
            }

            int lastIndex = _healingInstances.Count - 1;
            if (index == lastIndex)
            {
                _healingInstances.RemoveAt(lastIndex);
                return;
            }

            BuffInstance replacement = _healingInstances[lastIndex];
            _healingInstances[index] = replacement;
            replacement.HealingListIndex = index;
            _healingInstances.RemoveAt(lastIndex);
        }

        private void AddDamage(BuffInstance instance)
        {
            if (instance == null || instance.DamageListIndex >= 0)
            {
                return;
            }

            instance.DamageListIndex = _damageInstances.Count;
            _damageInstances.Add(instance);
        }

        private void RemoveDamage(BuffInstance instance)
        {
            if (instance == null || instance.DamageListIndex < 0)
            {
                return;
            }

            int index = instance.DamageListIndex;
            instance.DamageListIndex = -1;
            if (index >= _damageInstances.Count ||
                !ReferenceEquals(_damageInstances[index], instance))
            {
                return;
            }

            int lastIndex = _damageInstances.Count - 1;
            if (index == lastIndex)
            {
                _damageInstances.RemoveAt(lastIndex);
                return;
            }

            BuffInstance replacement = _damageInstances[lastIndex];
            _damageInstances[index] = replacement;
            replacement.DamageListIndex = index;
            _damageInstances.RemoveAt(lastIndex);
        }

    }
}
