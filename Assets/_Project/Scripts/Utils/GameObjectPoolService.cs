using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Utils
{
    /// <summary>对象从池中租出或归还时可实现的生命周期回调。</summary>
    public interface IPooledObject
    {
        void OnRentFromPool();
        void OnReturnToPool();
    }

    /// <summary>单个预制体池的创建与保留上限。</summary>
    public readonly struct PoolSettings
    {
        public int InitialCapacity { get; }
        public int MaximumRetained { get; }

        public PoolSettings(int initialCapacity, int maximumRetained)
        {
            InitialCapacity = Mathf.Max(0, initialCapacity);
            MaximumRetained = Mathf.Max(InitialCapacity, maximumRetained);
        }
    }

    /// <summary>单个预制体池的运行时占用信息。</summary>
    public readonly struct PoolUsage
    {
        public int TotalCount { get; }
        public int AvailableCount { get; }
        public int RentedCount { get; }

        public PoolUsage(int totalCount, int availableCount, int rentedCount)
        {
            TotalCount = totalCount;
            AvailableCount = availableCount;
            RentedCount = rentedCount;
        }
    }

    public interface IGameObjectPool : IDisposable
    {
        /// <summary>按指定配置租借一个预制体实例。</summary>
        /// <exception cref="InvalidOperationException">该预制体已有 Bucket 且请求配置与其不一致。</exception>
        GameObject Rent(
            GameObject prefab,
            PoolSettings settings,
            Transform parent = null,
            bool activate = true,
            Func<GameObject, GameObject> factory = null);

        bool Return(GameObject instance);

        /// <summary>按指定配置预热一个预制体池。</summary>
        /// <exception cref="InvalidOperationException">该预制体已有 Bucket 且请求配置与其不一致。</exception>
        void Prewarm(GameObject prefab, PoolSettings settings, int count, Func<GameObject, GameObject> factory = null);
        PoolUsage GetUsage(GameObject prefab);
        void Clear(GameObject prefab);
        void ClearAll();
    }

    /// <summary>
    /// 由根容器持有、供各场景子容器共享的 GameObject 池。
    /// 每个预制体独立维护容量、已租出实例和可用实例；同一 Bucket 的配置在显式清理前不可变。
    /// 归还未知或重复归还的对象会被拒绝。
    /// </summary>
    public sealed class GameObjectPoolService : IGameObjectPool
    {
        private sealed class Entry
        {
            public int InstanceId;
            public GameObject Instance;
            public Bucket Bucket;
            public IPooledObject[] LifecycleCallbacks;
        }

        private sealed class Bucket
        {
            public GameObject Prefab;
            public PoolSettings Settings;
            public Func<GameObject, GameObject> Factory;
            public readonly Stack<Entry> Available = new();
            public readonly Dictionary<int, Entry> Entries = new();
            public int RentedCount;
        }

        private readonly Dictionary<GameObject, Bucket> _buckets = new();
        private readonly Dictionary<int, Entry> _entriesByInstanceId = new();
        private readonly HashSet<int> _rentedInstanceIds = new();

        /// <summary>订阅场景卸载事件，及时移除被 Unity 自动销毁的池实例。</summary>
        public GameObjectPoolService()
        {
            SceneManager.sceneUnloaded += HandleSceneUnloaded;
        }

        public GameObject Rent(
            GameObject prefab,
            PoolSettings settings,
            Transform parent = null,
            bool activate = true,
            Func<GameObject, GameObject> factory = null)
        {
            if (prefab == null)
                return null;

            Bucket bucket = GetOrCreateBucket(prefab, settings, factory);
            RemoveDestroyedEntries(bucket);
            Entry entry = TakeAvailable(bucket) ?? CreateEntry(bucket);
            if (entry == null || entry.Instance == null)
                return null;

            int instanceId = entry.Instance.GetInstanceID();
            _rentedInstanceIds.Add(instanceId);
            bucket.RentedCount++;

            entry.Instance.transform.SetParent(parent, false);
            InvokeRentCallbacks(entry);
            entry.Instance.SetActive(activate);
            return entry.Instance;
        }

        public bool Return(GameObject instance)
        {
            if (ReferenceEquals(instance, null))
            {
                return false;
            }

            if (instance == null)
            {
                RemoveDestroyedEntries();
                return false;
            }

            int instanceId = instance.GetInstanceID();
            if (!_entriesByInstanceId.TryGetValue(instanceId, out Entry entry) ||
                !ReferenceEquals(entry.Instance, instance) ||
                !_rentedInstanceIds.Remove(instanceId))
            {
                return false;
            }

            Bucket bucket = entry.Bucket;
            bucket.RentedCount--;

            // 先停用对象，再执行复用回调，避免回调期间把已归还对象当作活动对象观察到。
            instance.SetActive(false);
            InvokeReturnCallbacks(entry);
            instance.transform.SetParent(null, false);

            if (bucket.Available.Count >= bucket.Settings.MaximumRetained)
            {
                RemoveEntry(entry);
                UnityEngine.Object.Destroy(instance);
                return true;
            }

            bucket.Available.Push(entry);
            return true;
        }

        public void Prewarm(
            GameObject prefab,
            PoolSettings settings,
            int count,
            Func<GameObject, GameObject> factory = null)
        {
            if (prefab == null)
                return;

            if (count <= 0)
            {
                // 已有 Bucket 时仍校验配置,避免无效调用掩盖同一预制体的配置冲突。
                if (_buckets.TryGetValue(prefab, out Bucket existingBucket))
                    ValidateBucketConfiguration(prefab, existingBucket, settings, factory);
                return;
            }

            Bucket bucket = GetOrCreateBucket(prefab, settings, factory);
            RemoveDestroyedEntries(bucket);
            int targetCount = Mathf.Min(count, bucket.Settings.MaximumRetained);
            while (bucket.Available.Count < targetCount)
            {
                Entry entry = CreateEntry(bucket);
                if (entry == null)
                    break;

                bucket.Available.Push(entry);
            }
        }

        public PoolUsage GetUsage(GameObject prefab)
        {
            if (prefab == null || !_buckets.TryGetValue(prefab, out Bucket bucket))
                return default;

            RemoveDestroyedEntries(bucket);
            return new PoolUsage(bucket.Entries.Count, bucket.Available.Count, bucket.RentedCount);
        }

        public void Clear(GameObject prefab)
        {
            if (ReferenceEquals(prefab, null) || !_buckets.Remove(prefab, out Bucket bucket))
                return;

            ClearBucket(bucket);
        }

        public void ClearAll()
        {
            var buckets = new List<Bucket>(_buckets.Values);
            for (int i = 0; i < buckets.Count; i++)
            {
                ClearBucket(buckets[i]);
            }

            _buckets.Clear();
            _entriesByInstanceId.Clear();
            _rentedInstanceIds.Clear();
        }

        public void Dispose()
        {
            SceneManager.sceneUnloaded -= HandleSceneUnloaded;
            ClearAll();
        }

        private Bucket GetOrCreateBucket(
            GameObject prefab,
            PoolSettings settings,
            Func<GameObject, GameObject> factory)
        {
            if (_buckets.TryGetValue(prefab, out Bucket bucket))
            {
                ValidateBucketConfiguration(prefab, bucket, settings, factory);
                return bucket;
            }

            bucket = new Bucket
            {
                Prefab = prefab,
                Settings = settings,
                Factory = factory
            };
            _buckets.Add(prefab, bucket);

            if (settings.InitialCapacity > 0)
                Prewarm(prefab, settings, settings.InitialCapacity, factory);

            return bucket;
        }

        private static void ValidateBucketConfiguration(
            GameObject prefab,
            Bucket bucket,
            PoolSettings settings,
            Func<GameObject, GameObject> factory)
        {
            bool settingsChanged =
                bucket.Settings.InitialCapacity != settings.InitialCapacity ||
                bucket.Settings.MaximumRetained != settings.MaximumRetained;
            bool factoryChanged = !Equals(bucket.Factory, factory);
            if (!settingsChanged && !factoryChanged)
                return;

            throw new InvalidOperationException(
                $"预制体 '{prefab.name}' 的对象池配置在 Bucket 生命周期内不可变。" +
                $"已有容量({bucket.Settings.InitialCapacity}, {bucket.Settings.MaximumRetained})," +
                $"请求容量({settings.InitialCapacity}, {settings.MaximumRetained});" +
                $"已有工厂:{DescribeFactory(bucket.Factory)},请求工厂:{DescribeFactory(factory)}。" +
                "如需更换配置,请先 Clear(prefab)。");
        }

        private static string DescribeFactory(Func<GameObject, GameObject> factory)
        {
            return factory == null
                ? "UnityEngine.Object.Instantiate"
                : factory.Method.Name;
        }

        private Entry TakeAvailable(Bucket bucket)
        {
            while (bucket.Available.Count > 0)
            {
                Entry entry = bucket.Available.Pop();
                if (entry.Instance != null)
                    return entry;

                RemoveEntry(entry);
            }

            return null;
        }

        private Entry CreateEntry(Bucket bucket)
        {
            GameObject instance = bucket.Factory != null
                ? bucket.Factory(bucket.Prefab)
                : UnityEngine.Object.Instantiate(bucket.Prefab);
            if (instance == null)
            {
                Debug.LogError($"[GameObjectPoolService] 无法创建预制体: {bucket.Prefab.name}");
                return null;
            }

            instance.SetActive(false);
            int instanceId = instance.GetInstanceID();
            if (_entriesByInstanceId.TryGetValue(instanceId, out Entry existingEntry))
            {
                if (existingEntry.Instance != null)
                {
                    DestroyInstance(instance);
                    return null;
                }

                RemoveEntry(existingEntry);
            }

            var entry = new Entry
            {
                InstanceId = instanceId,
                Instance = instance,
                Bucket = bucket,
                LifecycleCallbacks = FindLifecycleCallbacks(instance)
            };

            bucket.Entries.Add(instanceId, entry);
            _entriesByInstanceId.Add(instanceId, entry);
            return entry;
        }

        private void RemoveEntry(Entry entry)
        {
            if (entry == null)
                return;

            if (_rentedInstanceIds.Remove(entry.InstanceId))
            {
                entry.Bucket.RentedCount = Mathf.Max(0, entry.Bucket.RentedCount - 1);
            }

            _entriesByInstanceId.Remove(entry.InstanceId);
            entry.Bucket.Entries.Remove(entry.InstanceId);
        }

        private void RemoveDestroyedEntries()
        {
            var buckets = new List<Bucket>(_buckets.Values);
            for (int i = 0; i < buckets.Count; i++)
            {
                RemoveDestroyedEntries(buckets[i]);
            }
        }

        private void RemoveDestroyedEntries(Bucket bucket)
        {
            var availableEntries = new List<Entry>(bucket.Available.Count);
            while (bucket.Available.Count > 0)
            {
                Entry entry = bucket.Available.Pop();
                if (entry.Instance != null)
                {
                    availableEntries.Add(entry);
                }
                else
                {
                    RemoveEntry(entry);
                }
            }

            for (int i = availableEntries.Count - 1; i >= 0; i--)
            {
                bucket.Available.Push(availableEntries[i]);
            }

            var entries = new List<Entry>(bucket.Entries.Values);
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Instance == null)
                {
                    RemoveEntry(entries[i]);
                }
            }
        }

        private void ClearBucket(Bucket bucket)
        {
            var entries = new List<Entry>(bucket.Entries.Values);
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                _entriesByInstanceId.Remove(entry.InstanceId);
                _rentedInstanceIds.Remove(entry.InstanceId);
                if (entry.Instance != null)
                {
                    DestroyInstance(entry.Instance);
                }
            }

            bucket.Available.Clear();
            bucket.Entries.Clear();
            bucket.RentedCount = 0;
        }

        private void HandleSceneUnloaded(Scene _)
        {
            RemoveDestroyedEntries();
        }

        private static void DestroyInstance(GameObject instance)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object.DestroyImmediate(instance);
                return;
            }
#endif
            UnityEngine.Object.Destroy(instance);
        }

        private static IPooledObject[] FindLifecycleCallbacks(GameObject instance)
        {
            MonoBehaviour[] behaviours = instance.GetComponentsInChildren<MonoBehaviour>(true);
            var callbacks = new List<IPooledObject>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is IPooledObject callback)
                    callbacks.Add(callback);
            }

            return callbacks.ToArray();
        }

        private static void InvokeRentCallbacks(Entry entry)
        {
            for (int i = 0; i < entry.LifecycleCallbacks.Length; i++)
            {
                IPooledObject callback = entry.LifecycleCallbacks[i];
                if (callback is MonoBehaviour behaviour && behaviour != null)
                {
                    callback.OnRentFromPool();
                }
            }
        }

        private static void InvokeReturnCallbacks(Entry entry)
        {
            for (int i = 0; i < entry.LifecycleCallbacks.Length; i++)
            {
                IPooledObject callback = entry.LifecycleCallbacks[i];
                if (callback is MonoBehaviour behaviour && behaviour != null)
                {
                    callback.OnReturnToPool();
                }
            }
        }
    }
}
