using System;
using System.Collections.Generic;
using UnityEngine;
using Utils;
using VContainer;
using VContainer.Unity;

namespace EnemySystem.Spawning
{
    /// <summary>敌人预制体的场景级复用入口。</summary>
    public sealed class EnemyPool : IDisposable
    {
        private static readonly PoolSettings Settings = new(initialCapacity: 10, maximumRetained: 100);

        private readonly IGameObjectPool _pool;
        private readonly IObjectResolver _resolver;
        private readonly Func<GameObject, GameObject> _instantiate;
        private readonly HashSet<GameObject> _prefabs = new();

        [Inject]
        public EnemyPool(IGameObjectPool pool, IObjectResolver resolver)
        {
            _pool = pool;
            _resolver = resolver;
            _instantiate = InstantiateWithResolver;
        }

        public GameObject Get(GameObject prefab)
        {
            if (prefab == null)
                return null;

            _prefabs.Add(prefab);
            return _pool.Rent(
                prefab,
                Settings,
                activate: false,
                factory: _instantiate);
        }

        private GameObject InstantiateWithResolver(GameObject prefab)
        {
            return _resolver.Instantiate(prefab);
        }

        public void Release(GameObject instance)
        {
            if (instance == null)
                return;

            _pool.Return(instance);
        }

        public void Clear()
        {
            foreach (GameObject prefab in _prefabs)
                _pool.Clear(prefab);

            _prefabs.Clear();
        }

        public void Dispose()
        {
            Clear();
        }
    }
}
