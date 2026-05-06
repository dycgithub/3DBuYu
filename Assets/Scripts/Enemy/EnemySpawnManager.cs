using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// 敌人生成管理器
    /// 负责管理敌人的生成、波次控制
    /// </summary>
    public class EnemySpawnManager : MonoBehaviour
    {
        [Header("生成设置")]
        [Tooltip("波次配置")]
        public WaveConfig waveConfig;

        [Tooltip("生成点列表")]
        public List<Transform> spawnPoints = new List<Transform>();

        [Tooltip("默认生成点（如果没有指定）")]
        public Transform defaultSpawnPoint;

        [Header("生成池")]
        [Tooltip("普通敌人预制体")]
        public GameObject normalEnemyPrefab;

        [Tooltip("快速敌人预制体")]
        public GameObject fastEnemyPrefab;

        [Tooltip("坦克敌人预制体")]
        public GameObject tankEnemyPrefab;

        [Tooltip("飞行敌人预制体")]
        public GameObject flyingEnemyPrefab;

        [Header("生成控制")]
        [Tooltip("自动开始第一波")]
        public bool autoStart = true;

        [Tooltip("生成安全距离（玩家附近不生成）")]
        public float safeDistance = 10f;

        [Header("调试")]
        [Tooltip("显示调试信息")]
        public bool showDebugInfo = true;

        // 当前状态
        private int currentWaveNumber = 0;
        private WaveData currentWave;
        private bool isSpawning = false;
        private bool isWaveActive = false;
        private int enemiesSpawned = 0;
        private int enemiesKilled = 0;
        private int enemiesAlive = 0;

        // 运行时数据
        private Dictionary<EnemyType, GameObject> enemyPrefabs;
        private List<EnemyBase> activeEnemies = new List<EnemyBase>();
        private Transform playerTransform;

        #region 属性

        public int CurrentWaveNumber => currentWaveNumber;
        public int EnemiesAlive => enemiesAlive;
        public bool IsWaveActive => isWaveActive;
        public bool IsSpawning => isSpawning;

        #endregion

        #region 事件

        /// <summary>
        /// 波次开始事件
        /// </summary>
        public System.Action<int> OnWaveStarted;

        /// <summary>
        /// 波次结束事件
        /// </summary>
        public System.Action<int> OnWaveEnded;

        /// <summary>
        /// 敌人生成事件
        /// </summary>
        public System.Action<EnemyBase> OnEnemySpawned;

        /// <summary>
        /// 敌人死亡事件
        /// </summary>
        public System.Action<EnemyBase> OnEnemyDied;

        /// <summary>
        /// 所有波次完成事件
        /// </summary>
        public System.Action OnAllWavesCompleted;

        #endregion

        #region Unity生命周期

        private void Awake()
        {
            InitializePrefabs();
        }

        private void Start()
        {
            // 查找玩家
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }

            if (autoStart)
            {
                StartNextWave();
            }
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }

        private void OnDrawGizmos()
        {
            if (!showDebugInfo) return;

            // 绘制生成点
            Gizmos.color = Color.red;
            foreach (var point in spawnPoints)
            {
                if (point != null)
                {
                    Gizmos.DrawWireSphere(point.position, 1f);
                    Gizmos.DrawLine(point.position, point.position + Vector3.up * 2f);
                }
            }

            // 绘制安全距离
            if (playerTransform != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(playerTransform.position, safeDistance);
            }
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化敌人生成池
        /// </summary>
        private void InitializePrefabs()
        {
            enemyPrefabs = new Dictionary<EnemyType, GameObject>
            {
                { EnemyType.Normal, normalEnemyPrefab },
                { EnemyType.Fast, fastEnemyPrefab },
                { EnemyType.Tank, tankEnemyPrefab },
                { EnemyType.Flying, flyingEnemyPrefab }
            };
        }

        #endregion

        #region 波次控制

        /// <summary>
        /// 开始下一波
        /// </summary>
        public void StartNextWave()
        {
            if (waveConfig == null)
            {
                Debug.LogError("未配置波次数据！");
                return;
            }

            currentWaveNumber++;
            currentWave = waveConfig.GetWave(currentWaveNumber);

            if (currentWave == null)
            {
                Debug.Log("所有波次已完成！");
                OnAllWavesCompleted?.Invoke();
                return;
            }

            StartCoroutine(WaveCoroutine());
        }

        /// <summary>
        /// 波次协程
        /// </summary>
        private IEnumerator WaveCoroutine()
        {
            // 准备阶段
            isWaveActive = true;
            enemiesSpawned = 0;
            enemiesKilled = 0;

            Debug.Log($"波次 {currentWaveNumber} 准备中... {currentWave.waveName}");
            yield return new WaitForSeconds(currentWave.preparationTime);

            // 开始生成
            Debug.Log($"波次 {currentWaveNumber} 开始！");
            OnWaveStarted?.Invoke(currentWaveNumber);

            isSpawning = true;

            // 同时生成多种敌人
            List<Coroutine> spawnCoroutines = new List<Coroutine>();
            foreach (var enemyInfo in currentWave.enemies)
            {
                if (enemyInfo.enemyPrefab != null || enemyPrefabs.ContainsKey(enemyInfo.enemyType))
                {
                    spawnCoroutines.Add(StartCoroutine(SpawnEnemyTypeCoroutine(enemyInfo)));
                }
            }

            // 等待所有生成完成
            foreach (var coroutine in spawnCoroutines)
            {
                yield return coroutine;
            }

            isSpawning = false;
            Debug.Log($"波次 {currentWaveNumber} 生成完成，共生成 {enemiesSpawned} 个敌人");

            // 等待所有敌人被消灭
            yield return new WaitUntil(() => enemiesAlive <= 0);

            // 波次结束
            yield return new WaitForSeconds(currentWave.clearDelay);

            EndWave();
        }

        /// <summary>
        /// 生成特定类型敌人的协程
        /// </summary>
        private IEnumerator SpawnEnemyTypeCoroutine(WaveEnemyInfo enemyInfo)
        {
            // 延迟开始
            yield return new WaitForSeconds(enemyInfo.delayStart);

            for (int i = 0; i < enemyInfo.spawnCount; i++)
            {
                SpawnEnemy(enemyInfo.enemyType, enemyInfo.enemyPrefab);
                yield return new WaitForSeconds(enemyInfo.spawnInterval);
            }
        }

        /// <summary>
        /// 结束当前波次
        /// </summary>
        private void EndWave()
        {
            isWaveActive = false;
            Debug.Log($"波次 {currentWaveNumber} 结束！");
            OnWaveEnded?.Invoke(currentWaveNumber);

            // 检查是否还有下一波
            if (waveConfig.GetWave(currentWaveNumber + 1) != null || waveConfig.loopMode)
            {
                StartNextWave();
            }
            else
            {
                OnAllWavesCompleted?.Invoke();
            }
        }

        /// <summary>
        /// 跳过当前波次（调试用）
        /// </summary>
        [ContextMenu("跳过当前波次")]
        public void SkipCurrentWave()
        {
            if (isWaveActive)
            {
                StopAllCoroutines();

                // 消灭所有敌人
                foreach (var enemy in activeEnemies.ToArray())
                {
                    if (enemy != null)
                    {
                        enemy.TakeDamage(99999f);
                    }
                }

                EndWave();
            }
        }

        #endregion

        #region 敌人生成

        /// <summary>
        /// 生成单个敌人
        /// </summary>
        public EnemyBase SpawnEnemy(EnemyType type, GameObject customPrefab = null)
        {
            GameObject prefab = customPrefab ?? enemyPrefabs.GetValueOrDefault(type);
            if (prefab == null)
            {
                Debug.LogWarning($"未找到 {type} 类型的敌人预制体！");
                return null;
            }

            // 获取生成位置
            Vector3 spawnPosition = GetSpawnPosition();

            // 生成敌人
            GameObject enemyObj = Instantiate(prefab, spawnPosition, Quaternion.identity);
            EnemyBase enemy = enemyObj.GetComponent<EnemyBase>();

            if (enemy == null)
            {
                Debug.LogError("敌人预制体缺少 EnemyBase 组件！");
                Destroy(enemyObj);
                return null;
            }

            // 设置目标
            if (playerTransform != null)
            {
                enemy.SetTarget(playerTransform);
            }

            // 应用波次难度调整
            ApplyWaveDifficulty(enemy);

            // 注册事件
            activeEnemies.Add(enemy);
            enemiesSpawned++;
            enemiesAlive++;

            OnEnemySpawned?.Invoke(enemy);

            return enemy;
        }

        /// <summary>
        /// 应用波次难度
        /// </summary>
        private void ApplyWaveDifficulty(EnemyBase enemy)
        {
            if (currentWave == null) return;

            // 通过反射或直接修改（需要EnemyBase提供修改方法）
            // 这里简化处理，实际应该在EnemyBase中提供相应接口
            var healthField = enemy.GetType().GetField("maxHealth",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public);

            if (healthField != null)
            {
                float originalHealth = (float)healthField.GetValue(enemy);
                healthField.SetValue(enemy, originalHealth * currentWave.healthMultiplier);
            }
        }

        /// <summary>
        /// 获取生成位置
        /// </summary>
        private Vector3 GetSpawnPosition()
        {
            List<Transform> validSpawnPoints = new List<Transform>();

            // 筛选安全的生成点
            foreach (var point in spawnPoints)
            {
                if (point == null) continue;

                if (playerTransform == null)
                {
                    validSpawnPoints.Add(point);
                    continue;
                }

                float distance = Vector3.Distance(point.position, playerTransform.position);
                if (distance >= safeDistance)
                {
                    validSpawnPoints.Add(point);
                }
            }

            // 选择生成点
            if (validSpawnPoints.Count > 0)
            {
                Transform spawnPoint = validSpawnPoints[Random.Range(0, validSpawnPoints.Count)];
                return spawnPoint.position;
            }

            // 使用默认生成点
            if (defaultSpawnPoint != null)
            {
                return defaultSpawnPoint.position;
            }

            // 随机位置
            return transform.position + Random.insideUnitSphere * 10f;
        }

        #endregion

        #region 敌人管理

        /// <summary>
        /// 敌人死亡回调
        /// </summary>
        public void OnEnemyDeath(EnemyBase enemy)
        {
            if (activeEnemies.Contains(enemy))
            {
                activeEnemies.Remove(enemy);
                enemiesKilled++;
                enemiesAlive--;

                OnEnemyDied?.Invoke(enemy);
            }
        }

        /// <summary>
        /// 清除所有敌人
        /// </summary>
        [ContextMenu("清除所有敌人")]
        public void ClearAllEnemies()
        {
            foreach (var enemy in activeEnemies.ToArray())
            {
                if (enemy != null)
                {
                    Destroy(enemy.gameObject);
                }
            }

            activeEnemies.Clear();
            enemiesAlive = 0;
        }

        /// <summary>
        /// 获取最近敌人
        /// </summary>
        public EnemyBase GetNearestEnemy(Vector3 position)
        {
            EnemyBase nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (var enemy in activeEnemies)
            {
                if (enemy == null || enemy.IsDead) continue;

                float distance = Vector3.Distance(position, enemy.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = enemy;
                }
            }

            return nearest;
        }

        #endregion
    }
}
