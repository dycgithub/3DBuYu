using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace EnemySystem.Spawning
{
    public class EnemyPool
    {
        private readonly Dictionary<GameObject, ObjectPool<GameObject>> _pools = new();
        private readonly Dictionary<GameObject, GameObject> _prefabMap = new();

        public GameObject Get(GameObject prefab)
        {
            if (!_pools.TryGetValue(prefab, out var pool))
            {
                pool = new ObjectPool<GameObject>(
                    createFunc: () =>
                    {
                        var obj = Object.Instantiate(prefab);
                        obj.SetActive(false);
                        return obj;
                    },
                    actionOnGet: null,
                    actionOnRelease: obj => obj.SetActive(false),
                    actionOnDestroy: Object.Destroy,
                    collectionCheck: false,
                    defaultCapacity: 10,
                    maxSize: 100
                );
                _pools[prefab] = pool;
            }

            var instance = pool.Get();
            _prefabMap[instance] = prefab;
            return instance;
        }

        public void Release(GameObject obj)
        {
            if (!_prefabMap.TryGetValue(obj, out var prefab)) return;
            if (!_pools.TryGetValue(prefab, out var pool)) return;
            pool.Release(obj);
            _prefabMap.Remove(obj);
        }

        public void Clear()
        {
            foreach (var pool in _pools.Values)
                pool.Clear();
            _pools.Clear();
            _prefabMap.Clear();
        }
    }
}