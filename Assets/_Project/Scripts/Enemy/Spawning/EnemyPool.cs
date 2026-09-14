using System;
using System.Collections.Generic;
using UnityEngine;
using Utils;
using VContainer;
using VContainer.Unity;

namespace EnemySystem.Spawning
{
    /// <summary>敌人预制体的场景级复用入口；有容器时使用依赖注入实例化，无容器时回退到 Unity 实例化。</summary>
    public sealed class EnemyPool : IDisposable
    {
        private static readonly PoolSettings EnemyPoolSettings = new(
            initialCapacity: 10,
            maximumRetained: 100);

        private readonly IGameObjectPool _pool;
        private readonly IObjectResolver _resolver;
        private readonly Func<GameObject, GameObject> _instantiate;
        private readonly HashSet<GameObject> _prefabs = new();

        /// <summary>
        /// 创建敌人对象池并注入共享对象池和依赖解析器。
        /// </summary>
        /// <param name="pool">全局对象池服务。</param>
        /// <param name="resolver">用于实例化预制体的可选依赖解析器。</param>
        [Inject]
        public EnemyPool(
            IGameObjectPool pool,
            IObjectResolver resolver = null)
        {
            _pool = pool;
            _resolver = resolver;
            _instantiate = InstantiateWithResolver;
        }

        /// <summary>
        /// 从对象池租借一个敌人实例。
        /// </summary>
        /// <param name="prefab">敌人预制体。</param>
        /// <returns>未激活的敌人实例；预制体为空时返回 null。</returns>
        public GameObject RentEnemy(GameObject prefab)
        {
            if (prefab == null)
            {
                return null;
            }

            _prefabs.Add(prefab);
            return _pool.Rent(
                prefab,
                EnemyPoolSettings,
                activate: false,
                factory: _instantiate);
        }

        /// <summary>
        /// 使用依赖注入器实例化敌人预制体。
        /// </summary>
        /// <param name="prefab">敌人预制体。</param>
        /// <returns>实例化后的敌人对象。</returns>
        private GameObject InstantiateWithResolver(GameObject prefab)
        {
            return _resolver != null
                ? _resolver.Instantiate(prefab)
                : UnityEngine.Object.Instantiate(prefab);
        }

        /// <summary>
        /// 将敌人实例归还对象池。
        /// </summary>
        /// <param name="instance">待归还的敌人实例。</param>
        public void ReturnEnemy(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            _pool.Return(instance);
        }

        /// <summary>
        /// 清空已登记预制体对应的对象池。
        /// </summary>
        public void Clear()
        {
            foreach (GameObject prefab in _prefabs)
            {
                _pool.Clear(prefab);
            }

            _prefabs.Clear();
        }

        /// <summary>
        /// 释放对象池持有的全部敌人实例。
        /// </summary>
        public void Dispose()
        {
            Clear();
        }
    }
}
