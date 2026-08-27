using System;
using System.Collections.Generic;
using UnityEngine;

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
        GameObject Rent(
            GameObject prefab,
            PoolSettings settings,
            Transform parent = null,
            bool activate = true,
            Func<GameObject, GameObject> factory = null);

        bool Return(GameObject instance);
        void Prewarm(GameObject prefab, PoolSettings settings, int count, Func<GameObject, GameObject> factory = null);
        PoolUsage GetUsage(GameObject prefab);
        void Clear(GameObject prefab);
        void ClearAll();
    }

    /// <summary>
    /// 战斗场景共用的 GameObject 池。
    /// 每个预制体独立维护容量、已租出实例和可用实例；归还未知或重复归还的对象会被拒绝。
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
            if (instance == null)
                return false;

            int instanceId = instance.GetInstanceID();
            if (!_entriesByInstanceId.TryGetValue(instanceId, out Entry entry) ||
                !_rentedInstanceIds.Remove(instanceId))
            {
                return false;
            }

            Bucket bucket = entry.Bucket;
            bucket.RentedCount--;
            InvokeReturnCallbacks(entry);
            instance.SetActive(false);
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
            if (prefab == null || count <= 0)
                return;

            Bucket bucket = GetOrCreateBucket(prefab, settings, factory);
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

            return new PoolUsage(bucket.Entries.Count, bucket.Available.Count, bucket.RentedCount);
        }

        public void Clear(GameObject prefab)
        {
            if (prefab == null || !_buckets.Remove(prefab, out Bucket bucket))
                return;

            var entries = new List<Entry>(bucket.Entries.Values);
            for (int i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[i];
                _entriesByInstanceId.Remove(entry.InstanceId);
                _rentedInstanceIds.Remove(entry.InstanceId);
                if (entry.Instance != null)
                    UnityEngine.Object.Destroy(entry.Instance);
            }

            bucket.Available.Clear();
            bucket.Entries.Clear();
        }

        public void ClearAll()
        {
            var prefabs = new List<GameObject>(_buckets.Keys);
            for (int i = 0; i < prefabs.Count; i++)
                Clear(prefabs[i]);

            _rentedInstanceIds.Clear();
        }

        public void Dispose()
        {
            ClearAll();
        }

        private Bucket GetOrCreateBucket(
            GameObject prefab,
            PoolSettings settings,
            Func<GameObject, GameObject> factory)
        {
            if (_buckets.TryGetValue(prefab, out Bucket bucket))
                return bucket;

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

            _entriesByInstanceId.Remove(entry.InstanceId);
            _rentedInstanceIds.Remove(entry.InstanceId);
            entry.Bucket.Entries.Remove(entry.InstanceId);
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
                if (callback != null)
                    callback.OnRentFromPool();
            }
        }

        private static void InvokeReturnCallbacks(Entry entry)
        {
            for (int i = 0; i < entry.LifecycleCallbacks.Length; i++)
            {
                IPooledObject callback = entry.LifecycleCallbacks[i];
                if (callback != null)
                    callback.OnReturnToPool();
            }
        }
    }
}
