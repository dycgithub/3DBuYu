using System.Collections.Generic;
using Interfaces;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>管理目标状态、活动实例索引以及目标租借代次。</summary>
    internal sealed class BuffStateStore
    {
        private readonly Dictionary<IBuffTarget, BuffTargetState> _targetStates = new();
        private readonly Dictionary<int, BuffInstance> _activeInstances = new();
        private int _nextGeneration;
        private int _nextInstanceId;

        internal IEnumerable<BuffTargetState> Targets => _targetStates.Values;

        /// <summary>查找目标当前登记的状态。</summary>
        internal bool TryGetTargetState(IBuffTarget target, out BuffTargetState state)
        {
            return _targetStates.TryGetValue(target, out state);
        }

        /// <summary>创建新一代目标状态。</summary>
        internal BuffTargetState RegisterTarget(IBuffTarget target, IHealable healable)
        {
            _nextGeneration++;
            if (_nextGeneration <= 0)
            {
                _nextGeneration = 1;
            }

            BuffTargetState state = new(target, _nextGeneration, healable);
            _targetStates.Add(target, state);
            return state;
        }

        /// <summary>从登记表移除目标状态。</summary>
        internal void RemoveTarget(IBuffTarget target)
        {
            _targetStates.Remove(target);
        }

        /// <summary>创建并登记一个新的活动 Buff 实例。</summary>
        internal BuffInstance CreateInstance(
            BuffTargetState state,
            BuffDefinitionSO definition,
            int sourceInstanceId,
            float currentTime)
        {
            _nextInstanceId++;
            if (_nextInstanceId <= 0)
            {
                _nextInstanceId = 1;
            }

            float durationSeconds = definition.DurationSeconds;
            BuffInstance instance = new()
            {
                Definition = definition,
                SourceInstanceId = sourceInstanceId,
                RemainingDurationSeconds = durationSeconds,
                StackCount = 1,
                ExpireAt = durationSeconds > 0f
                    ? currentTime + durationSeconds
                    : float.PositiveInfinity,
                LastEffectTime = currentTime,
            };

            return TrackInstance(state, instance);
        }

        /// <summary>创建无需静态资产的独立持续伤害实例。</summary>
        internal BuffInstance CreateDamageOverTimeInstance(
            BuffTargetState state,
            int sourceInstanceId,
            float damagePerSecond,
            float durationSeconds,
            float currentTime)
        {
            BuffInstance instance = new()
            {
                IsDamageOverTime = true,
                SourceInstanceId = sourceInstanceId,
                DamagePerSecond = damagePerSecond,
                RemainingDurationSeconds = durationSeconds,
                StackCount = 1,
                ExpireAt = currentTime + durationSeconds,
                LastEffectTime = currentTime,
            };

            return TrackInstance(state, instance);
        }

        /// <summary>移除活动实例的全局索引。</summary>
        internal void RemoveInstance(BuffInstance instance)
        {
            _activeInstances.Remove(instance.InstanceId);
        }

        private BuffInstance TrackInstance(BuffTargetState state, BuffInstance instance)
        {
            _nextInstanceId++;
            if (_nextInstanceId <= 0)
            {
                _nextInstanceId = 1;
            }

            instance.InstanceId = _nextInstanceId;
            instance.TargetGeneration = state.Generation;
            instance.Target = state.Target;
            instance.IsActive = true;
            _activeInstances.Add(instance.InstanceId, instance);
            return instance;
        }

        /// <summary>按实例身份与目标代次解析当前目标状态。</summary>
        internal BuffTargetState FindInstanceState(BuffInstance instance)
        {
            if (instance == null || !instance.IsActive ||
                !_activeInstances.TryGetValue(instance.InstanceId, out BuffInstance indexed) ||
                !ReferenceEquals(indexed, instance) ||
                !IsReferenceAlive(instance.Target) ||
                !_targetStates.TryGetValue(instance.Target, out BuffTargetState state) ||
                state.Generation != instance.TargetGeneration)
            {
                return null;
            }

            return state;
        }

        /// <summary>清空目标和实例索引。</summary>
        internal void Clear()
        {
            _targetStates.Clear();
            _activeInstances.Clear();
        }

        /// <summary>检查普通引用或 Unity 对象引用是否仍有效。</summary>
        internal static bool IsReferenceAlive(object value)
        {
            if (value == null)
            {
                return false;
            }

            return !(value is Object unityObject) || unityObject != null;
        }
    }
}
