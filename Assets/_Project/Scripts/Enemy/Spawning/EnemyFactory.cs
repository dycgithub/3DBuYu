using FlockingSystem.ECS;
using UnityEngine;
using VContainer;

namespace EnemySystem.Spawning
{
    /// <summary>
    /// 创建、初始化和释放敌人对象，并为每个敌人建立 ECS Flocking 绑定。
    /// 不负责群游计算；群游行为由 <see cref="EnemyFlockRuntimeService" /> 负责。
    /// </summary>
    public class EnemyFactory
    {
        private readonly EnemyPool _pool;
        private readonly EnemyFlockRuntimeService _flockRuntime;

        [Inject]
        public EnemyFactory(
            EnemyPool pool,
            EnemyFlockRuntimeService flockRuntime)
        {
            _pool = pool;
            _flockRuntime = flockRuntime;
        }

        /// <summary>
        /// 从对象池取得敌人并绑定到 ECS Flocking。
        /// </summary>
        /// <param name="prefab">敌人源预制体，同时用于提取 ECS 渲染资源。</param>
        /// <param name="position">敌人的世界坐标。</param>
        /// <param name="currentWave">当前波次配置。</param>
        /// <returns>已初始化的敌人；资源缺失或 ECS 绑定失败时返回 <c>null</c>。</returns>
        public Enemy Create(
            GameObject prefab,
            Vector3 position,
            WaveData currentWave)
        {
            if (prefab == null)
                return null;

            GameObject obj = _pool.Get(prefab);
            if (obj == null)
                return null;

            Enemy enemy = obj.GetComponent<Enemy>();
            if (enemy == null)
            {
                _pool.Release(obj);
                return null;
            }

            enemy.SourcePrefab = prefab;
            enemy.ResetForReuse();
            enemy.ApplyStats(currentWave.healthMultiplier, currentWave.speedMultiplier);

            Quaternion rotation = prefab.transform.rotation;
            obj.transform.SetPositionAndRotation(position, rotation);

            EnemyFlockBridge bridge = obj.GetComponent<EnemyFlockBridge>();
            if (bridge == null)
                bridge = obj.AddComponent<EnemyFlockBridge>();

            if (!_flockRuntime.TryAcquire(
                    bridge,
                    prefab,
                    enemy.EnemyType,
                    enemy.SpeedMultiplier,
                    position,
                    rotation))
            {
                _pool.Release(obj);
                return null;
            }

            obj.SetActive(true);
            return enemy;
        }

        /// <summary>
        /// 解除 ECS 绑定后将敌人归还对象池。
        /// </summary>
        public void Release(Enemy enemy)
        {
            if (enemy == null)
                return;

            EnemyFlockBridge bridge = enemy.GetComponent<EnemyFlockBridge>();
            if (bridge != null)
                _flockRuntime.Release(bridge);

            _pool.Release(enemy.gameObject);
        }
    }
}
