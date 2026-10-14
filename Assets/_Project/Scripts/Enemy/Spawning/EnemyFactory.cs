using System;
using FlockingSystem.ECS;
using UnityEngine;
using VContainer;
using EnemySystem;
using EnemySystem.Components;

namespace EnemySystem.Spawning
{
    /// <summary>
    /// 创建、初始化和释放敌人对象，并校验核心与类型专属机制组件。
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
        /// 从对象池取得敌人，激活查询注册后再绑定 ECS，确保空间槽位有效。
        /// </summary>
        /// <param name="prefab">敌人源预制体，同时用于提取 ECS 渲染资源。</param>
        /// <param name="spawnCategory">本次生成实例的分类。</param>
        /// <param name="position">敌人的世界坐标。</param>
        /// <param name="healthMultiplier">本局难度生命倍率。</param>
        /// <param name="speedMultiplier">本局难度速度倍率。</param>
        /// <param name="initialRotation">本次生成使用的世界初始旋转。</param>
        /// <returns>已初始化的敌人；资源缺失或 ECS 绑定失败时返回 <c>null</c>。</returns>
        public Enemy CreateEnemy(
            GameObject prefab,
            EnemySpawnCategory spawnCategory,
            Vector3 position,
            float healthMultiplier,
            float speedMultiplier,
            Quaternion initialRotation)
        {
            if (prefab == null)
            {
                return null;
            }

            if (!ValidatePrefab(prefab))
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
            enemy.InitializeForSpawn(healthMultiplier, speedMultiplier, spawnCategory);
            if (!enemy.HasConfiguration || enemy.MaxHealth <= 0f)
            {
                _pool.ReturnEnemy(enemyInstance);
                return null;
            }

            enemyInstance.transform.SetPositionAndRotation(position, initialRotation);

            EnemyFlockBridge bridge = enemyInstance.GetComponent<EnemyFlockBridge>();
            if (bridge == null)
            {
                _pool.ReturnEnemy(enemyInstance);
                return null;
            }

            enemyInstance.SetActive(true);
            if (!_flockRuntime.TryAcquireAgent(
                    bridge,
                    prefab,
                    enemy.EnemyType,
                    enemy.GetMovementSpeedMultiplier(),
                    position,
                    initialRotation))
            {
                _pool.ReturnEnemy(enemyInstance);
                return null;
            }

            return enemy;
        }

        /// <summary>在对象池租借前验证敌人的核心和类型专属组件组合。</summary>
        /// <param name="prefab">待生成的敌人预制体。</param>
        /// <returns>组合完整时返回 true。</returns>
        private static bool ValidatePrefab(GameObject prefab)
        {
            Enemy enemy = prefab.GetComponent<Enemy>();
            if (enemy == null)
            {
                Debug.LogError($"[EnemyFactory] {prefab.name}: 缺少 Enemy 组件。", prefab);
                return false;
            }

            if (!enemy.HasConfiguration)
            {
                Debug.LogError($"[EnemyFactory] {prefab.name}: 缺少 EnemyAttributesSO 配置。", prefab);
                return false;
            }

            if (prefab.GetComponent<EnemyFlockBridge>() == null)
            {
                Debug.LogError($"[EnemyFactory] {prefab.name}: 缺少 EnemyFlockBridge 组件。", prefab);
                return false;
            }

            if (prefab.GetComponent<EnemyDeathRewardComponent>() == null)
            {
                Debug.LogError(
                    $"[EnemyFactory] {prefab.name}: 缺少 EnemyDeathRewardComponent，无法接入击杀奖励链。",
                    prefab);
                return false;
            }

            return ValidateMechanic(enemy);
        }

        /// <summary>验证敌人类型要求的机制组件。</summary>
        /// <param name="enemy">待生成的敌人。</param>
        /// <returns>必需机制组件存在时返回 true。</returns>
        private static bool ValidateMechanic(Enemy enemy)
        {
            Type mechanicType = enemy.EnemyType switch
            {
                EnemyType.Fast => typeof(EnemyDodgeComponent),
                EnemyType.Tank => typeof(EnemyShieldComponent),
                EnemyType.Assault => typeof(EnemyAssaultComponent),
                EnemyType.Growth => typeof(EnemyGrowthComponent),
                EnemyType.Reflect => typeof(EnemyReflectComponent),
                EnemyType.Exploder => typeof(EnemyDeathExplosionComponent),
                EnemyType.Shooter => typeof(EnemyShooterComponent),
                EnemyType.Slow or EnemyType.Healer or EnemyType.StrongHealer =>
                    typeof(EnemyAreaEffectComponent),
                EnemyType.Mother => typeof(EnemyMotherComponent),
                _ => null
            };

            if (mechanicType == null || enemy.GetComponent(mechanicType) != null)
            {
                return true;
            }

            Debug.LogError(
                $"[EnemyFactory] {enemy.name}: 缺少 {mechanicType.Name} 组件。",
                enemy);
            return false;
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
