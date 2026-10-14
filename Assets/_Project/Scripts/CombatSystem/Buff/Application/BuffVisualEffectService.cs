using System.Collections.Generic;
using Interfaces;
using Services;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>将活动 Buff 实例关联到池化视效，并在实例结束时归还对象。</summary>
    internal sealed class BuffVisualEffectService
    {
        private readonly struct ActiveVisual
        {
            internal readonly GameObject Prefab;
            internal readonly GameObject Instance;

            internal ActiveVisual(GameObject prefab, GameObject instance)
            {
                Prefab = prefab;
                Instance = instance;
            }
        }

        private readonly IPooledEffectService _effects;
        private readonly Dictionary<int, ActiveVisual> _activeVisuals = new();
        private readonly List<int> _instanceIds = new();

        internal BuffVisualEffectService(IPooledEffectService effects)
        {
            _effects = effects;
        }

        /// <summary>为带视效配置的 Buff 实例创建一个持续附着效果。</summary>
        internal void Start(BuffInstance instance, IBuffTarget target)
        {
            if (_effects == null || instance?.Definition?.VisualEffectPrefab == null)
            {
                return;
            }

            Stop(instance);
            Transform parent = target is Component component ? component.transform : null;
            Vector3 position = target is IDamageable damageable
                ? damageable.Position
                : parent != null ? parent.position : Vector3.zero;
            GameObject prefab = instance.Definition.VisualEffectPrefab;
            GameObject visual = _effects.Play(
                prefab,
                position,
                Quaternion.identity,
                Vector3.one,
                0f,
                parent);
            if (visual != null)
            {
                _activeVisuals[instance.InstanceId] = new ActiveVisual(prefab, visual);
            }
        }

        /// <summary>刷新定义时只重建实际更换预制体的视效。</summary>
        internal void Refresh(BuffInstance instance, IBuffTarget target)
        {
            if (instance == null || instance.Definition == null)
            {
                return;
            }

            if (instance.Definition.VisualEffectPrefab == null)
            {
                Stop(instance);
                return;
            }

            if (_activeVisuals.TryGetValue(instance.InstanceId, out ActiveVisual visual) &&
                visual.Prefab == instance.Definition.VisualEffectPrefab)
            {
                return;
            }

            Start(instance, target);
        }

        /// <summary>归还指定 Buff 实例持有的视效对象。</summary>
        internal void Stop(BuffInstance instance)
        {
            if (instance == null || !_activeVisuals.Remove(instance.InstanceId, out ActiveVisual visual))
            {
                return;
            }

            _effects?.Stop(visual.Instance);
        }

        /// <summary>清理所有仍由 Buff 实例持有的视觉对象。</summary>
        internal void Clear()
        {
            _instanceIds.Clear();
            foreach (int instanceId in _activeVisuals.Keys)
            {
                _instanceIds.Add(instanceId);
            }

            for (int index = 0; index < _instanceIds.Count; index++)
            {
                if (_activeVisuals.Remove(_instanceIds[index], out ActiveVisual visual))
                {
                    _effects?.Stop(visual.Instance);
                }
            }

            _instanceIds.Clear();
        }
    }
}
