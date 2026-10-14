using System;
using System.Collections.Generic;
using Interfaces;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>保存单个目标的 Buff、叠层索引和属性合成缓存。</summary>
    internal sealed class BuffTargetState
    {
        private readonly List<BuffInstance> _activeInstances = new();
        private readonly Dictionary<BuffMergeKey, BuffInstance> _mergeableInstances = new();
        private readonly Dictionary<BuffModifierType, float> _modifierMultipliers = new();
        private Dictionary<int, float> _permanentMovementMultipliers;

        internal IBuffTarget Target { get; }
        internal IHealable Healable { get; }
        internal int Generation { get; }
        internal IReadOnlyList<BuffInstance> ActiveInstances => _activeInstances;
        internal bool HasPermanentMovementMultipliers =>
            _permanentMovementMultipliers != null && _permanentMovementMultipliers.Count > 0;

        private bool _modifierCacheDirty = true;

        /// <summary>创建一代目标状态及其属性缓存。</summary>
        internal BuffTargetState(IBuffTarget target, int generation, IHealable healable)
        {
            Target = target;
            Generation = generation;
            Healable = healable;
        }

        /// <summary>查找相同来源、属性类别和叠层组的活动实例。</summary>
        internal bool TryGetMergeable(
            BuffDefinitionSO definition,
            int sourceInstanceId,
            out BuffInstance instance)
        {
            return _mergeableInstances.TryGetValue(
                new BuffMergeKey(definition, sourceInstanceId),
                out instance);
        }

        /// <summary>登记实例，并按合并键加入可叠层索引。</summary>
        internal void AddInstance(BuffInstance instance)
        {
            _activeInstances.Add(instance);
            if (instance.StackPolicy != BuffStackPolicy.Independent)
            {
                AddMergeIndex(instance);
            }
        }

        /// <summary>从目标集合和叠层索引移除实例。</summary>
        internal void RemoveInstance(BuffInstance instance)
        {
            RemoveMergeIndex(instance);
            _activeInstances.Remove(instance);
        }

        /// <summary>从叠层索引移除实例当前使用的键。</summary>
        internal void RemoveMergeIndex(BuffInstance instance)
        {
            if (instance == null || instance.StackPolicy == BuffStackPolicy.Independent)
            {
                return;
            }

            BuffMergeKey key = new(instance.Definition, instance.SourceInstanceId);
            if (_mergeableInstances.TryGetValue(key, out BuffInstance indexed) &&
                ReferenceEquals(indexed, instance))
            {
                _mergeableInstances.Remove(key);
            }
        }

        /// <summary>把实例当前定义对应的键写入叠层索引。</summary>
        internal void AddMergeIndex(BuffInstance instance)
        {
            if (instance == null || instance.StackPolicy == BuffStackPolicy.Independent)
            {
                return;
            }

            _mergeableInstances[new BuffMergeKey(
                instance.Definition,
                instance.SourceInstanceId)] = instance;
        }

        /// <summary>检查目标是否已有指定属性类别的活动 Buff。</summary>
        internal bool HasModifier(BuffModifierType modifierType)
        {
            for (int index = 0; index < _activeInstances.Count; index++)
            {
                BuffInstance instance = _activeInstances[index];
                if (instance.IsActive && instance.ModifierType == modifierType)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>取得属性类别的合成倍率，缓存失效时先重建。</summary>
        internal float GetModifierMultiplier(BuffModifierType modifierType)
        {
            if (_modifierCacheDirty)
            {
                RebuildModifierCache();
            }

            return _modifierMultipliers.TryGetValue(modifierType, out float multiplier)
                ? multiplier
                : 1f;
        }

        /// <summary>设置一个来源的永久移动速度倍率。</summary>
        internal void SetPermanentMovementMultiplier(int sourceInstanceId, float multiplier)
        {
            _permanentMovementMultipliers ??= new Dictionary<int, float>();
            _permanentMovementMultipliers[sourceInstanceId] = Mathf.Max(0f, multiplier);
        }

        /// <summary>移除指定来源的永久移动速度倍率。</summary>
        internal bool RemovePermanentMovementMultiplier(int sourceInstanceId)
        {
            return _permanentMovementMultipliers != null &&
                _permanentMovementMultipliers.Remove(sourceInstanceId);
        }

        /// <summary>清除所有永久移动速度倍率。</summary>
        internal bool ClearPermanentMovementMultipliers()
        {
            if (!HasPermanentMovementMultipliers)
            {
                return false;
            }

            _permanentMovementMultipliers.Clear();
            return true;
        }

        /// <summary>清除临时 Buff 索引和缓存内容。</summary>
        internal void ClearTemporaryInstances()
        {
            _activeInstances.Clear();
            _mergeableInstances.Clear();
            _modifierMultipliers.Clear();
            _modifierCacheDirty = true;
        }

        /// <summary>标记属性缓存失效并通知目标更新对应属性。</summary>
        internal void MarkModifierChanged(BuffModifierType modifierType)
        {
            _modifierCacheDirty = true;
            NotifyModifierChanged(modifierType);
        }

        /// <summary>向仍然有效且启用的目标发出属性变化通知。</summary>
        internal void NotifyModifierChanged(BuffModifierType modifierType)
        {
            if (BuffStateStore.IsReferenceAlive(Target) && Target.IsActive)
            {
                Target.OnBuffModifiersChanged(modifierType);
            }
        }

        private void RebuildModifierCache()
        {
            _modifierMultipliers.Clear();
            for (int index = 0; index < _activeInstances.Count; index++)
            {
                BuffInstance instance = _activeInstances[index];
                if (!instance.IsActive)
                {
                    continue;
                }

                float value = Mathf.Max(0f, instance.ModifierValue);
                float multiplier = 1f;
                for (int stack = 0; stack < Mathf.Max(1, instance.StackCount); stack++)
                {
                    multiplier *= value;
                }

                BuffModifierType modifierType = instance.ModifierType;
                if (_modifierMultipliers.TryGetValue(modifierType, out float existing))
                {
                    _modifierMultipliers[modifierType] = existing * multiplier;
                }
                else
                {
                    _modifierMultipliers.Add(modifierType, multiplier);
                }
            }

            if (_permanentMovementMultipliers != null)
            {
                float permanentMultiplier = 1f;
                foreach (float multiplier in _permanentMovementMultipliers.Values)
                {
                    permanentMultiplier *= multiplier;
                }

                BuffModifierType movementSpeed = BuffModifierType.MovementSpeedMultiplier;
                if (_modifierMultipliers.TryGetValue(movementSpeed, out float existing))
                {
                    _modifierMultipliers[movementSpeed] = existing * permanentMultiplier;
                }
                else if (_permanentMovementMultipliers.Count > 0)
                {
                    _modifierMultipliers.Add(movementSpeed, permanentMultiplier);
                }
            }

            _modifierCacheDirty = false;
        }

        private readonly struct BuffMergeKey : IEquatable<BuffMergeKey>
        {
            private readonly BuffModifierType _modifierType;
            private readonly string _stackGroupId;
            private readonly int _sourceInstanceId;

            internal BuffMergeKey(BuffDefinitionSO definition, int sourceInstanceId)
            {
                _modifierType = definition != null ? definition.ModifierType : default;
                _stackGroupId = definition != null &&
                    !string.IsNullOrWhiteSpace(definition.StackGroupId)
                    ? definition.StackGroupId
                    : null;
                _sourceInstanceId = sourceInstanceId;
            }

            public bool Equals(BuffMergeKey other)
            {
                return _modifierType == other._modifierType &&
                    _sourceInstanceId == other._sourceInstanceId &&
                    string.Equals(_stackGroupId, other._stackGroupId, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is BuffMergeKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)_modifierType;
                    hash = (hash * 397) ^ (_stackGroupId != null
                        ? StringComparer.Ordinal.GetHashCode(_stackGroupId)
                        : 0);
                    return (hash * 397) ^ _sourceInstanceId;
                }
            }
        }
    }
}
