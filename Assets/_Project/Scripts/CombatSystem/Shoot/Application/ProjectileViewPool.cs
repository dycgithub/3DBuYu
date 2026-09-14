using System;
using System.Collections.Generic;
using UnityEngine;
using Utils;

namespace CombatSystem
{
    /// <summary>
    /// 管理投射物视觉实例的对象池适配器。
    /// </summary>
    public sealed class ProjectileViewPool : IDisposable
    {
        private readonly IGameObjectPool _pool;
        private readonly HashSet<GameObject> _prefabs = new();

        /// <summary>创建投射物视觉对象池。</summary>
        /// <param name="pool">提供视觉实例租借和归还能力的对象池。</param>
        public ProjectileViewPool(IGameObjectPool pool)
        {
            _pool = pool;
        }

        /// <summary>按投射物配置租借并定位一个视觉实例。</summary>
        /// <param name="projectileProfile">提供视觉预制体和对象池容量的配置。</param>
        /// <param name="position">视觉实例的世界坐标。</param>
        /// <param name="rotation">视觉实例的世界旋转。</param>
        /// <returns>租借的视觉实例；配置或对象池无效时返回 <c>null</c>。</returns>
        public GameObject RentProjectileView(
            ProjectileProfileSO projectileProfile,
            Vector3 position,
            Quaternion rotation)
        {
            if (_pool == null || projectileProfile?.VisualDefinition?.ProjectilePrefab == null)
            {
                return null;
            }

            GameObject prefab = projectileProfile.VisualDefinition.ProjectilePrefab;
            _prefabs.Add(prefab);
            GameObject instance = _pool.Rent(
                prefab,
                new PoolSettings(
                    projectileProfile.VisualDefinition.PrewarmInstanceCount,
                    Mathf.Max(1, projectileProfile.VisualDefinition.MaximumRetainedInstanceCount)));
            if (instance == null)
            {
                return null;
            }

            instance.transform.SetPositionAndRotation(position, rotation);
            return instance;
        }

        /// <summary>归还一个投射物视觉实例。</summary>
        /// <param name="projectileViewInstance">要归还的视觉实例。</param>
        public void ReturnProjectileView(GameObject projectileViewInstance)
        {
            if (projectileViewInstance != null)
            {
                _pool?.Return(projectileViewInstance);
            }
        }

        /// <summary>清理所有已登记预制体的视觉对象池。</summary>
        public void ClearAllProjectileViews()
        {
            if (_pool == null)
            {
                return;
            }

            foreach (GameObject prefab in _prefabs)
            {
                _pool.Clear(prefab);
            }
            _prefabs.Clear();
        }

        /// <summary>释放对象池中的视觉对象。</summary>
        public void Dispose() => ClearAllProjectileViews();
    }
}
