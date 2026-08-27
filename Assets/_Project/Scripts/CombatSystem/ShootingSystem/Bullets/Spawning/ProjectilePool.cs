using System;
using System.Collections.Generic;
using UnityEngine;
using Utils;

namespace CombatSystem
{
    /// <summary>子弹表现对象的场景级复用入口。</summary>
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
            if (profile?.Visual?.Prefab == null)
                return null;

            GameObject prefab = profile.Visual.Prefab;
            _prefabs.Add(prefab);
            var settings = new PoolSettings(
                profile.Visual.PrewarmCount,
                Mathf.Max(1, profile.Visual.MaximumRetained));
            GameObject instance = _pool.Rent(prefab, settings);
            if (instance == null)
                return null;

            instance.transform.SetPositionAndRotation(position, rotation);
            return instance;
        }

        public void Return(BulletProfile profile, GameObject instance)
        {
            if (instance == null)
                return;

            _pool.Return(instance);
        }

        public PoolUsage GetUsage(BulletProfile profile)
        {
            return profile?.Visual?.Prefab != null
                ? _pool.GetUsage(profile.Visual.Prefab)
                : default;
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
