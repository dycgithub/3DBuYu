using System;
using System.Collections.Generic;
using CombatSystem;
using EnemySystem;
using Interfaces;
using TMPro;
using UnityEngine;
using Utils;
using VContainer;
using VContainer.Unity;

namespace _Project.UI.Damage
{
    /// <summary>
    /// 集中处理敌人伤害跳字的聚合、裁剪、动画和对象池生命周期。
    /// <para>事件只创建或更新池中视图，不为每个敌人创建 UI 或 Update。</para>
    /// </summary>
    public sealed class DamageNumberSystem : ITickable, IDisposable
    {
        private sealed class Entry
        {
            public int TargetId;
            public DamageNumberPriority Priority;
            public float Damage;
            public float LastHitTime;
            public float StartTime;
            public Vector3 WorldPosition;
            public Vector2 Offset;
            public DamageNumberView View;
            public bool Active;
        }

        private readonly struct AggregationKey : IEquatable<AggregationKey>
        {
            private readonly int _targetId;
            private readonly DamageNumberPriority _priority;

            public AggregationKey(int targetId, DamageNumberPriority priority)
            {
                _targetId = targetId;
                _priority = priority;
            }

            public bool Equals(AggregationKey other)
            {
                return _targetId == other._targetId && _priority == other._priority;
            }

            public override bool Equals(object obj)
            {
                return obj is AggregationKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(_targetId, (int)_priority);
            }
        }

        private readonly IDamageEventSource _damageEventSource;
        private readonly IGameObjectPool _pool;
        private readonly IObjectResolver _resolver;
        private readonly Func<GameObject, GameObject> _viewFactory;
        private readonly DamageNumberLayer _layer;
        private readonly DamageNumberConfigSO _config;
        private GameObject _poolPrefab;
        private readonly Dictionary<AggregationKey, int> _aggregation = new();
        private readonly List<Entry> _entries = new();
        private readonly Stack<int> _freeIndices = new();
        private int _sequence;
        private bool _disposed;
        private bool _warnedUnavailable;

        /// <summary>注入统一伤害事件、场景 UI 层和全局对象池。</summary>
        public DamageNumberSystem(
            IDamageEventSource damageEventSource,
            IGameObjectPool pool,
            IObjectResolver resolver)
        {
            _damageEventSource = damageEventSource;
            _pool = pool;
            _resolver = resolver;
            _viewFactory = InstantiateView;
            _layer = resolver.TryResolve(out DamageNumberLayer layer) ? layer : null;
            _config = resolver.TryResolve(out DamageNumberConfigSO config) ? config : null;
            _damageEventSource.DamageApplied += HandleDamageApplied;
        }

        /// <summary>集中推进所有可见跳字的屏幕动画。</summary>
        public void Tick()
        {
            if (_disposed || _entries.Count == 0)
            {
                return;
            }

            DamageNumberConfigSO config = _config;
            float duration = config != null ? Mathf.Max(0.01f, config.duration) : 0.75f;
            Camera camera = _layer != null ? _layer.WorldCamera : null;
            RectTransform root = _layer != null ? _layer.Root : null;
            Canvas canvas = _layer != null ? _layer.Canvas : null;
            if (camera == null || root == null || canvas == null)
            {
                return;
            }

            float currentTime = Time.time;
            for (int index = 0; index < _entries.Count; index++)
            {
                Entry entry = _entries[index];
                if (entry == null || !entry.Active)
                {
                    continue;
                }

                if (entry.View == null)
                {
                    Release(index);
                    continue;
                }

                float normalizedTime = Mathf.Clamp01((currentTime - entry.StartTime) / duration);
                if (normalizedTime >= 1f)
                {
                    Release(index);
                    continue;
                }

                Vector3 screenPosition = camera.WorldToScreenPoint(entry.WorldPosition);
                if (screenPosition.z <= 0f)
                {
                    entry.View.SetPose(Vector2.zero, 0f);
                    continue;
                }

                Vector2 offset = entry.Offset;
                float horizontalOffset = config != null ? config.horizontalOffset : 48f;
                float verticalOffset = config != null ? config.verticalOffset : 24f;
                float verticalDrift = config != null ? config.verticalDrift : 36f;
                screenPosition += new Vector3(
                    offset.x * horizontalOffset,
                    verticalOffset + normalizedTime * verticalDrift,
                    0f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    root,
                    screenPosition,
                    canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                    out Vector2 localPosition);
                entry.View.SetPose(localPosition, 1f - normalizedTime * normalizedTime);
            }
        }

        /// <summary>注销事件并归还全部池中跳字。</summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _damageEventSource.DamageApplied -= HandleDamageApplied;
            for (int index = 0; index < _entries.Count; index++)
            {
                if (_entries[index] != null && _entries[index].Active)
                {
                    Release(index);
                }
            }

            _aggregation.Clear();
            _entries.Clear();
            _freeIndices.Clear();
            ClearPoolBucket();
        }

        /// <summary>只消费敌人的实际扣血事件。</summary>
        private void HandleDamageApplied(DamageEvent damageEvent)
        {
            if (_disposed || damageEvent.Target is not Enemy enemy ||
                damageEvent.AppliedDamage <= 0f || !CanShow(enemy, damageEvent.HitPosition))
            {
                return;
            }

            if (!TryGetViewPrefab(out DamageNumberView viewPrefab))
            {
                return;
            }

            DamageNumberPriority priority = DamageNumberMath.GetPriority(damageEvent.WasCritical);
            AggregationKey key = new(damageEvent.TargetInstanceId, priority);
            float currentTime = Time.time;
            if (priority == DamageNumberPriority.Normal &&
                _aggregation.TryGetValue(key, out int existingIndex) &&
                IsActive(existingIndex))
            {
                Entry existing = _entries[existingIndex];
                if (existing.View == null)
                {
                    Release(existingIndex);
                }
                else
                {
                    float elapsed = currentTime - existing.LastHitTime;
                    if (DamageNumberMath.CanAggregate(
                        existing.TargetId,
                        damageEvent.TargetInstanceId,
                        existing.Priority,
                        priority,
                        elapsed,
                        GetAggregationWindow()))
                    {
                        existing.Damage += damageEvent.AppliedDamage;
                        existing.LastHitTime = currentTime;
                        existing.StartTime = currentTime;
                        existing.WorldPosition = damageEvent.HitPosition;
                        existing.View.SetValue(existing.Damage, existing.Priority);
                        return;
                    }
                }
            }

            int index = Acquire(priority);
            if (index < 0)
            {
                return;
            }

            Entry entry = _entries[index];
            entry.TargetId = damageEvent.TargetInstanceId;
            entry.Priority = priority;
            entry.Damage = damageEvent.AppliedDamage;
            entry.LastHitTime = currentTime;
            entry.StartTime = currentTime;
            entry.WorldPosition = damageEvent.HitPosition;
            entry.Offset = DamageNumberMath.GetOffset(
                damageEvent.TargetInstanceId,
                _sequence++);
            entry.View = RentView(viewPrefab);
            if (entry.View == null)
            {
                entry.Active = false;
                _freeIndices.Push(index);
                return;
            }

            entry.Active = true;
            entry.View.SetValue(entry.Damage, entry.Priority);
            _aggregation[key] = index;
        }

        /// <summary>按距离和相机可见性裁剪跳字事件。</summary>
        private bool CanShow(Enemy enemy, Vector3 worldPosition)
        {
            if (_layer == null || _layer.WorldCamera == null || _layer.Root == null)
            {
                WarnUnavailable();
                return false;
            }

            Camera camera = _layer.WorldCamera;
            float maximumDistance = _config != null ? Mathf.Max(0f, _config.maximumDistance) : 80f;
            if (maximumDistance > 0f &&
                (worldPosition - camera.transform.position).sqrMagnitude > maximumDistance * maximumDistance)
            {
                return false;
            }

            Vector3 screenPosition = camera.WorldToScreenPoint(worldPosition);
            return screenPosition.z > 0f && camera.pixelRect.Contains(screenPosition);
        }

        /// <summary>从现有槽位中取得空闲槽位或淘汰低优先级跳字。</summary>
        private int Acquire(DamageNumberPriority priority)
        {
            if (_freeIndices.Count > 0)
            {
                return _freeIndices.Pop();
            }

            int maximumVisible = _config != null ? Mathf.Max(1, _config.maximumVisible) : 150;
            if (_entries.Count < maximumVisible)
            {
                _entries.Add(new Entry());
                return _entries.Count - 1;
            }

            int replacement = FindReplacement(priority);
            if (replacement < 0)
            {
                return -1;
            }

            Release(replacement);
            return _freeIndices.Pop();
        }

        /// <summary>寻找可以被新事件替换的最低优先级跳字。</summary>
        private int FindReplacement(DamageNumberPriority priority)
        {
            int candidate = -1;
            DamageNumberPriority candidatePriority = priority;
            for (int index = 0; index < _entries.Count; index++)
            {
                Entry entry = _entries[index];
                if (entry == null || !entry.Active ||
                    !DamageNumberMath.HasHigherPriority(priority, entry.Priority))
                {
                    continue;
                }

                if (candidate < 0 || entry.Priority < candidatePriority)
                {
                    candidate = index;
                    candidatePriority = entry.Priority;
                }
            }

            return candidate;
        }

        /// <summary>租借一个 TMP 跳字视图并完成依赖注入。</summary>
        private DamageNumberView RentView(DamageNumberView prefab)
        {
            if (prefab == null || _layer == null)
            {
                return null;
            }

            _poolPrefab = prefab.gameObject;
            Transform parent = _layer.Root;
            int initialCapacity = _config != null ? Mathf.Max(0, _config.initialPoolCapacity) : 32;
            int maximumRetained = _config != null ? Mathf.Max(1, _config.maximumRetained) : 300;
            GameObject instance = _pool.Rent(
                prefab.gameObject,
                new PoolSettings(initialCapacity, maximumRetained),
                parent,
                true,
                _viewFactory);
            if (instance == null)
            {
                return null;
            }

            DamageNumberView view = instance.GetComponent<DamageNumberView>();
            if (view == null || !view.IsReady)
            {
                _pool.Return(instance);
                return null;
            }

            return view;
        }

        /// <summary>清理本系统独占的跳字 Bucket，避免跨场景保留旧 factory。</summary>
        private void ClearPoolBucket()
        {
            if (_poolPrefab == null)
            {
                return;
            }

            _pool.Clear(_poolPrefab);
            _poolPrefab = null;
        }

        /// <summary>通过场景容器实例化跳字视图。</summary>
        private GameObject InstantiateView(GameObject source)
        {
            return _resolver.Instantiate(source);
        }

        /// <summary>释放一个跳字视图并清理其聚合键。</summary>
        private void Release(int index)
        {
            if (!IsActive(index))
            {
                return;
            }

            Entry entry = _entries[index];
            AggregationKey key = new(entry.TargetId, entry.Priority);
            if (_aggregation.TryGetValue(key, out int mappedIndex) && mappedIndex == index)
            {
                _aggregation.Remove(key);
            }

            DamageNumberView view = entry.View;
            if (view != null)
            {
                view.ResetView();
                _pool.Return(view.gameObject);
            }

            entry.View = null;
            entry.Active = false;
            _freeIndices.Push(index);
        }

        /// <summary>判断槽位是否仍持有活动视图。</summary>
        private bool IsActive(int index)
        {
            return index >= 0 && index < _entries.Count &&
                _entries[index] != null && _entries[index].Active;
        }

        /// <summary>读取配置中的聚合窗口。</summary>
        private float GetAggregationWindow()
        {
            return _config != null ? Mathf.Max(0f, _config.aggregationWindow) : 0.12f;
        }

        /// <summary>读取场景中配置的跳字预制体。</summary>
        private bool TryGetViewPrefab(out DamageNumberView prefab)
        {
            prefab = _layer != null ? _layer.ViewPrefab : null;
            if (prefab != null && prefab.IsReady)
            {
                return true;
            }

            WarnUnavailable();
            return false;
        }

        /// <summary>避免每次命中重复输出缺失 UI 配置警告。</summary>
        private void WarnUnavailable()
        {
            if (_warnedUnavailable)
            {
                return;
            }

            _warnedUnavailable = true;
            Debug.LogWarning(
                "[DamageNumberSystem] 未配置 DamageNumberLayer、Camera 或 DamageNumberView，跳字已停用。");
        }
    }
}
