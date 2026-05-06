using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GameSystem
{
    /// <summary>
    /// 游戏状态
    /// </summary>
    public enum GameState
    {
        Menu,       // 主菜单
        Playing,    // 游戏中
        Paused,     // 暂停
        GameOver,   // 游戏结束
        Victory     // 胜利
    }

    /// <summary>
    /// 游戏难度
    /// </summary>
    public enum Difficulty
    {
        Easy,       // 简单
        Normal,     // 普通
        Hard,       // 困难
        Nightmare   // 噩梦
    }

    /// <summary>
    /// 游戏管理器
    /// 游戏的中央控制器，管理游戏状态、流程和全局系统
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Header("游戏设置")]
        [Tooltip("当前难度")]
        public Difficulty currentDifficulty = Difficulty.Normal;

        [Tooltip("玩家预制体")]
        public GameObject playerPrefab;

        [Tooltip("玩家出生点")]
        public Transform playerSpawnPoint;

        [Tooltip("是否自动开始游戏")]
        public bool autoStartGame = false;

        [Header("系统引用")]
        [Tooltip("敌人生成管理器")]
        public EnemySystem.EnemySpawnManager spawnManager;

        [Tooltip("资源管理器")]
        public ResourceManager resourceManager;

        [Tooltip("特效管理器")]
        public EffectSystem.EffectManager effectManager;

        [Tooltip("音频管理器")]
        public AudioManager audioManager;

        // 单例
        public static GameManager Instance { get; private set; }

        // 当前状态
        private GameState currentState = GameState.Menu;
        private GameObject playerInstance;
        private bool isInitialized = false;

        // 游戏统计
        private float gameStartTime;
        private float sessionPlayTime;
        private int totalWavesCompleted;

        #region 属性

        public GameState CurrentState => currentState;
        public GameObject PlayerInstance => playerInstance;
        public float SessionPlayTime => sessionPlayTime;
        public bool IsPlaying => currentState == GameState.Playing;
        public bool IsPaused => currentState == GameState.Paused;

        #endregion

        #region 事件

        /// <summary>
        /// 游戏状态改变事件
        /// </summary>
        public event Action<GameState, GameState> OnGameStateChanged;

        /// <summary>
        /// 游戏开始事件
        /// </summary>
        public event Action OnGameStarted;

        /// <summary>
        /// 游戏暂停事件
        /// </summary>
        public event Action OnGamePaused;

        /// <summary>
        /// 游戏恢复事件
        /// </summary>
        public event Action OnGameResumed;

        /// <summary>
        /// 游戏结束事件
        /// </summary>
        public event Action OnGameOver;

        /// <summary>
        /// 玩家死亡事件
        /// </summary>
        public event Action OnPlayerDied;

        /// <summary>
        /// 波次完成事件
        /// </summary>
        public event Action<int> OnWaveCompleted;

        /// <summary>
        /// 敌人被击杀事件
        /// </summary>
        public event Action<EnemySystem.EnemyBase> OnEnemyKilledEvent;

        #endregion

        #region Unity生命周期

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            Initialize();

            if (autoStartGame)
            {
                StartGame();
            }
        }

        private void Update()
        {
            if (currentState == GameState.Playing)
            {
                sessionPlayTime += Time.deltaTime;
                UpdateGame();
            }

            // 暂停控制
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                TogglePause();
            }
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause && currentState == GameState.Playing)
            {
                PauseGame();
            }
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化游戏
        /// </summary>
        private void Initialize()
        {
            if (isInitialized) return;

            // 查找系统引用
            FindSystemReferences();

            // 订阅事件
            SubscribeToEvents();

            isInitialized = true;
            currentState = GameState.Menu;

            Debug.Log("游戏管理器初始化完成");
        }

        /// <summary>
        /// 查找系统引用
        /// </summary>
        private void FindSystemReferences()
        {
            if (spawnManager == null)
                spawnManager = FindObjectOfType<EnemySystem.EnemySpawnManager>();

            if (resourceManager == null)
                resourceManager = FindObjectOfType<ResourceManager>();

            if (effectManager == null)
                effectManager = FindObjectOfType<EffectSystem.EffectManager>();

            if (audioManager == null)
                audioManager = FindObjectOfType<AudioManager>();
        }

        /// <summary>
        /// 订阅事件
        /// </summary>
        private void SubscribeToEvents()
        {
            // 订阅敌人生成器事件
            if (spawnManager != null)
            {
                spawnManager.OnWaveEnded += OnWaveEnd;
                spawnManager.OnEnemyDied += OnEnemyDiedHandler;
            }
        }

        #endregion

        #region 游戏流程控制

        /// <summary>
        /// 开始游戏
        /// </summary>
        public void StartGame()
        {
            if (currentState == GameState.Playing) return;

            ChangeState(GameState.Playing);

            // 重置统计
            gameStartTime = Time.time;
            sessionPlayTime = 0f;
            totalWavesCompleted = 0;

            // 生成玩家
            SpawnPlayer();

            // 开始第一波
            if (spawnManager != null)
            {
                spawnManager.StartNextWave();
            }

            // 播放背景音乐
            audioManager?.PlayBGM(BGMType.Game, true);

            OnGameStarted?.Invoke();

            Debug.Log("游戏开始！");
        }

        /// <summary>
        /// 暂停游戏
        /// </summary>
        public void PauseGame()
        {
            if (currentState != GameState.Playing) return;

            ChangeState(GameState.Paused);
            Time.timeScale = 0f;

            OnGamePaused?.Invoke();

            Debug.Log("游戏暂停");
        }

        /// <summary>
        /// 恢复游戏
        /// </summary>
        public void ResumeGame()
        {
            if (currentState != GameState.Paused) return;

            ChangeState(GameState.Playing);
            Time.timeScale = 1f;

            OnGameResumed?.Invoke();

            Debug.Log("游戏恢复");
        }

        /// <summary>
        /// 切换暂停状态
        /// </summary>
        public void TogglePause()
        {
            if (currentState == GameState.Playing)
            {
                PauseGame();
            }
            else if (currentState == GameState.Paused)
            {
                ResumeGame();
            }
        }

        /// <summary>
        /// 游戏结束
        /// </summary>
        public void GameOver()
        {
            if (currentState == GameState.GameOver) return;

            ChangeState(GameState.GameOver);

            // 保存数据
            SaveGame();

            // 播放音效
            audioManager?.PlaySFXByName("GameOver");

            OnGameOver?.Invoke();

            Debug.Log("游戏结束！");
        }

        /// <summary>
        /// 返回主菜单
        /// </summary>
        public void ReturnToMenu()
        {
            ChangeState(GameState.Menu);
            Time.timeScale = 1f;

            // 清理游戏状态
            CleanupGame();

            Debug.Log("返回主菜单");
        }

        /// <summary>
        /// 重启游戏
        /// </summary>
        public void RestartGame()
        {
            // 清理当前游戏
            CleanupGame();

            // 重新开始
            StartGame();

            Debug.Log("游戏重启");
        }

        /// <summary>
        /// 退出游戏
        /// </summary>
        public void QuitGame()
        {
            // 保存数据
            SaveGame();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        #endregion

        #region 状态管理

        /// <summary>
        /// 切换状态
        /// </summary>
        private void ChangeState(GameState newState)
        {
            GameState oldState = currentState;
            currentState = newState;

            OnGameStateChanged?.Invoke(oldState, newState);

            Debug.Log($"游戏状态: {oldState} -> {newState}");
        }

        #endregion

        #region 玩家管理

        /// <summary>
        /// 生成玩家
        /// </summary>
        private void SpawnPlayer()
        {
            if (playerPrefab == null)
            {
                Debug.LogWarning("未设置玩家预制体！");
                return;
            }

            Vector3 spawnPos = playerSpawnPoint != null ?
                playerSpawnPoint.position : Vector3.zero;

            playerInstance = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
            playerInstance.tag = "Player";

            Debug.Log("玩家已生成");
        }

        /// <summary>
        /// 玩家死亡回调
        /// </summary>
        public void OnPlayerDeath()
        {
            OnPlayerDied?.Invoke();

            // 延迟游戏结束
            Invoke(nameof(GameOver), 2f);

            Debug.Log("玩家死亡！");
        }

        #endregion

        #region 敌人事件

        /// <summary>
        /// 波次结束回调
        /// </summary>
        private void OnWaveEnd(int waveNumber)
        {
            totalWavesCompleted++;

            OnWaveCompleted?.Invoke(waveNumber);

            Debug.Log($"波次 {waveNumber} 完成！");
        }

        /// <summary>
        /// 敌人死亡回调
        /// </summary>
        private void OnEnemyDiedHandler(EnemySystem.EnemyBase enemy)
        {
            // 记录击杀
            resourceManager?.RecordKill(enemy);

            OnEnemyKilledEvent?.Invoke(enemy);
        }

        /// <summary>
        /// 敌人被击杀（由EnemyBase调用）
        /// </summary>
        public void OnEnemyKilled(EnemySystem.EnemyBase enemy)
        {
            // 这个方法由EnemyBase.OnDeath调用
            // 实际的击杀统计在OnEnemyDiedHandler中处理
        }

        #endregion

        #region 游戏更新

        /// <summary>
        /// 每帧更新游戏逻辑
        /// </summary>
        private void UpdateGame()
        {
            // 在这里处理需要每帧更新的游戏逻辑
        }

        #endregion

        #region 存档/读档

        /// <summary>
        /// 保存游戏
        /// </summary>
        public void SaveGame()
        {
            var saveData = new GameSaveData
            {
                totalPlayTime = sessionPlayTime,
                wavesCompleted = totalWavesCompleted,
                difficulty = currentDifficulty,
                lastPlayDate = DateTime.Now.ToString()
            };

            SaveSystem.SaveGameData(saveData);
            resourceManager?.SaveData();

            Debug.Log("游戏已保存");
        }

        /// <summary>
        /// 加载游戏
        /// </summary>
        public void LoadGame()
        {
            var saveData = SaveSystem.LoadGameData();
            if (saveData != null)
            {
                currentDifficulty = saveData.difficulty;
                totalWavesCompleted = saveData.wavesCompleted;

                Debug.Log("游戏数据已加载");
            }
        }

        #endregion

        #region 清理

        /// <summary>
        /// 清理游戏状态
        /// </summary>
        private void CleanupGame()
        {
            // 销毁玩家
            if (playerInstance != null)
            {
                Destroy(playerInstance);
                playerInstance = null;
            }

            // 清理敌人
            spawnManager?.ClearAllEnemies();

            // 清理掉落物
            DropManager.Instance?.ClearAllDrops();

            // 重置时间
            Time.timeScale = 1f;

            Debug.Log("游戏状态已清理");
        }

        #endregion

        #region 难度设置

        /// <summary>
        /// 设置游戏难度
        /// </summary>
        public void SetDifficulty(Difficulty difficulty)
        {
            currentDifficulty = difficulty;

            // 应用难度设置
            ApplyDifficultySettings();

            Debug.Log($"游戏难度设置为: {difficulty}");
        }

        /// <summary>
        /// 应用难度设置
        /// </summary>
        private void ApplyDifficultySettings()
        {
            switch (currentDifficulty)
            {
                case Difficulty.Easy:
                    // 敌人血量减少，玩家伤害增加
                    break;
                case Difficulty.Normal:
                    // 标准设置
                    break;
                case Difficulty.Hard:
                    // 敌人血量增加，玩家伤害减少
                    break;
                case Difficulty.Nightmare:
                    // 极高难度
                    break;
            }
        }

        /// <summary>
        /// 获取难度倍率
        /// </summary>
        public float GetDifficultyMultiplier()
        {
            return currentDifficulty switch
            {
                Difficulty.Easy => 0.7f,
                Difficulty.Normal => 1f,
                Difficulty.Hard => 1.5f,
                Difficulty.Nightmare => 2.5f,
                _ => 1f
            };
        }

        #endregion

        #region 调试

        [ContextMenu("立即游戏结束")]
        private void DebugGameOver()
        {
            GameOver();
        }

        [ContextMenu("增加100金币")]
        private void DebugAddCoins()
        {
            resourceManager?.AddCoins(100, "调试");
        }

        [ContextMenu("跳过当前波次")]
        private void DebugSkipWave()
        {
            spawnManager?.SkipCurrentWave();
        }

        #endregion
    }

    /// <summary>
    /// 游戏存档数据
    /// </summary>
    [Serializable]
    public class GameSaveData
    {
        public float totalPlayTime;
        public int wavesCompleted;
        public Difficulty difficulty;
        public string lastPlayDate;
    }
}
