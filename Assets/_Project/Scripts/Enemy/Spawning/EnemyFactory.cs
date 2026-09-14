using FlockingSystem.ECS;
using UnityEngine;
using VContainer;
using EnemySystem;

namespace EnemySystem.Spawning
{
    /// <summary>
    /// 创建、初始化和释放敌人对象，并为每个敌人建立 ECS Flocking 绑定。
    /// <para>当前敌人不包含战斗技能，本类不负责组合技能或注入技能依赖。</para>
    /// <para>本类不负责群游计算；群游行为由 <see cref="EnemyFlockRuntimeService" /> 负责。</para>
    /// </summary>
    public class EnemyFactory
    {
        private readonly EnemyPool _pool;
        private readonly EnemyFlockRuntimeService _flockRuntime;

        /// <summary>
        /// 创建敌人工厂并注入敌人对象池和 ECS Flocking 服务。
        /// </summary>
        /// <param name="pool">敌人对象池。</param>
        /// <param name="flockRuntime">负责建立 ECS Flocking 绑定的服务。</param>
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
        /// <param name="healthMultiplier">本局难度生命倍率。</param>
        /// <param name="speedMultiplier">本局难度速度倍率。</param>
        /// <returns>已初始化的敌人；资源缺失或 ECS 绑定失败时返回 <c>null</c>。</returns>
        public Enemy CreateEnemy(
            GameObject prefab,
            Vector3 position,
            float healthMultiplier, float speedMultiplier)
        {
            if (prefab == null)
            {
                return null;
            }

            GameObject enemyInstance = _pool.RentEnemy(prefab);
            if (enemyInstance == null)
            {
                return null;
            }

            Enemy enemy = enemyInstance.GetComponent<Enemy>();
            if (enemy == null)
            {
                _pool.ReturnEnemy(enemyInstance);
                return null;
            }

            enemy.SourcePrefab = prefab;
            enemy.InitializeForSpawn(healthMultiplier, speedMultiplier);
            if (!enemy.HasConfiguration || enemy.MaxHealth <= 0f)
            {
                _pool.ReturnEnemy(enemyInstance);
                return null;
            }

            Quaternion rotation = prefab.transform.rotation;
            enemyInstance.transform.SetPositionAndRotation(position, rotation);

            EnemyFlockBridge bridge = enemyInstance.GetComponent<EnemyFlockBridge>();
            if (bridge == null)
            {
                _pool.ReturnEnemy(enemyInstance);
                return null;
            }

            if (!_flockRuntime.TryAcquireAgent(
                    bridge,
                    prefab,
                    enemy.EnemyType,
                    enemy.GetMovementSpeedMultiplier(),
                    position,
                    rotation))
            {
                _pool.ReturnEnemy(enemyInstance);
                return null;
            }

            enemyInstance.SetActive(true);
            return enemy;
        }

        /// <summary>
        /// 解除 ECS 绑定后将敌人归还对象池。
        /// </summary>
        public void ReleaseEnemy(Enemy enemy)
        {
            if (enemy == null)
            {
                return;
            }

            EnemyFlockBridge bridge = enemy.GetComponent<EnemyFlockBridge>();
            if (bridge != null)
            {
                _flockRuntime.ReleaseAgent(bridge);
            }

            _pool.ReturnEnemy(enemy.gameObject);
        }
    }
}
