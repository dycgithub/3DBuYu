using System;
using System.Collections.Generic;
using UnityEngine;
using Utils;

namespace CombatSystem
{
    public sealed class ProjectilePool : IDisposable
    {
        private readonly IGameObjectPool _pool;
        private readonly HashSet<GameObject> _prefabs = new();

        public ProjectilePool(IGameObjectPool pool)
        {
            _pool = pool;
        }

        public GameObject Rent(BulletProfile profile, Vector3 position, Quaternion rotation)
        {
            if (_pool == null || profile?.Visual?.Prefab == null)
                return null;

            GameObject prefab = profile.Visual.Prefab;
            _prefabs.Add(prefab);
            GameObject instance = _pool.Rent(
                prefab,
                new PoolSettings(profile.Visual.PrewarmCount, Mathf.Max(1, profile.Visual.MaximumRetained)));
            if (instance == null)
                return null;

            instance.transform.SetPositionAndRotation(position, rotation);
            return instance;
        }

        public void Return(BulletProfile profile, GameObject instance)
        {
            if (instance != null)
                _pool?.Return(instance);
        }

        public void Clear()
        {
            if (_pool == null)
                return;
            foreach (GameObject prefab in _prefabs)
                _pool.Clear(prefab);
            _prefabs.Clear();
        }

        public void Dispose() => Clear();
    }
}
