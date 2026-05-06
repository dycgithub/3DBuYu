using System;
using UnityEngine;

namespace GameSystem
{
    /// <summary>
    /// 资源类型
    /// </summary>
    public enum ResourceType
    {
        Coin,       // 金币
        Experience, // 经验值
        Gem,        // 宝石
        Energy      // 能量
    }

    /// <summary>
    /// 资源管理器
    /// 管理游戏中的所有资源（金币、经验等）
    /// </summary>
    public class ResourceManager : MonoBehaviour
    {
        [Header("初始值")]
        [Tooltip("初始金币")]
        [SerializeField]
        private int initialCoins = 0;

        [Tooltip("初始经验值")]
        [SerializeField]
        private int initialExperience = 0;

        [Tooltip("初始宝石")]
        [SerializeField]
        private int initialGems = 0;

        [Header("设置")]
        [Tooltip("最大金币")]
        [SerializeField]
        private int maxCoins = 999999;

        [Tooltip("最大经验值")]
        [SerializeField]
        private int maxExperience = int.MaxValue;

        [Tooltip("是否自动保存")]
        [SerializeField]
        private bool autoSave = true;

        [Tooltip("自动保存间隔（秒）")]
        [SerializeField]
        private float autoSaveInterval = 60f;

        // 单例
        public static ResourceManager Instance { get; private set; }

        // 当前资源值
        private int currentCoins;
        private int currentExperience;
        private int currentGems;
        private int currentEnergy;

        // 统计
        private int totalCoinsEarned;
        private int totalCoinsSpent;
        private int enemiesKilled;

        // 自动保存计时
        private float autoSaveTimer;

        #region 属性

        public int Coins => currentCoins;
        public int Experience => currentExperience;
        public int Gems => currentGems;
        public int Energy => currentEnergy;
        public int TotalCoinsEarned => totalCoinsEarned;
        public int TotalCoinsSpent => totalCoinsSpent;
        public int EnemiesKilled => enemiesKilled;

        #endregion

        #region 事件

        /// <summary>
        /// 资源改变事件 (类型, 当前值, 变化量)
        /// </summary>
        public event Action<ResourceType, int, int> OnResourceChanged;

        /// <summary>
        /// 金币改变事件 (当前值, 变化量)
        /// </summary>
        public event Action<int, int> OnCoinsChanged;

        /// <summary>
        /// 经验值改变事件 (当前值, 变化量)
        /// </summary>
        public event Action<int, int> OnExperienceChanged;

        /// <summary>
        /// 宝石改变事件 (当前值, 变化量)
        /// </summary>
        public event Action<int, int> OnGemsChanged;

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
        }

        private void Update()
        {
            if (autoSave)
            {
                autoSaveTimer += Time.deltaTime;
                if (autoSaveTimer >= autoSaveInterval)
                {
                    SaveData();
                    autoSaveTimer = 0f;
                }
            }
        }

        private void OnApplicationQuit()
        {
            if (autoSave)
            {
                SaveData();
            }
        }

        #endregion

        #region 初始化

        private void Initialize()
        {
            // 尝试加载存档
            if (!LoadData())
            {
                // 使用初始值
                currentCoins = initialCoins;
                currentExperience = initialExperience;
                currentGems = initialGems;
            }

            totalCoinsEarned = 0;
            totalCoinsSpent = 0;
            enemiesKilled = 0;
        }

        #endregion

        #region 资源操作

        /// <summary>
        /// 添加金币
        /// </summary>
        /// <param name="amount">数量</param>
        /// <param name="source">来源（用于统计）</param>
        public void AddCoins(int amount, string source = "")
        {
            if (amount <= 0) return;

            int oldCoins = currentCoins;
            currentCoins = Mathf.Min(currentCoins + amount, maxCoins);
            int actualAdded = currentCoins - oldCoins;

            totalCoinsEarned += actualAdded;

            OnCoinsChanged?.Invoke(currentCoins, actualAdded);
            OnResourceChanged?.Invoke(ResourceType.Coin, currentCoins, actualAdded);

            Debug.Log($"获得 {actualAdded} 金币 ({source})");
        }

        /// <summary>
        /// 消费金币
        /// </summary>
        public bool SpendCoins(int amount, string reason = "")
        {
            if (amount <= 0) return true;
            if (currentCoins < amount) return false;

            currentCoins -= amount;
            totalCoinsSpent += amount;

            OnCoinsChanged?.Invoke(currentCoins, -amount);
            OnResourceChanged?.Invoke(ResourceType.Coin, currentCoins, -amount);

            Debug.Log($"消费 {amount} 金币 ({reason})");
            return true;
        }

        /// <summary>
        /// 检查是否有足够金币
        /// </summary>
        public bool HasEnoughCoins(int amount) => currentCoins >= amount;

        /// <summary>
        /// 添加经验值
        /// </summary>
        public void AddExperience(int amount, string source = "")
        {
            if (amount <= 0) return;

            int oldExp = currentExperience;
            currentExperience = Mathf.Min(currentExperience + amount, maxExperience);
            int actualAdded = currentExperience - oldExp;

            OnExperienceChanged?.Invoke(currentExperience, actualAdded);
            OnResourceChanged?.Invoke(ResourceType.Experience, currentExperience, actualAdded);

            Debug.Log($"获得 {actualAdded} 经验 ({source})");
        }

        /// <summary>
        /// 添加宝石
        /// </summary>
        public void AddGems(int amount, string source = "")
        {
            if (amount <= 0) return;

            currentGems += amount;

            OnGemsChanged?.Invoke(currentGems, amount);
            OnResourceChanged?.Invoke(ResourceType.Gem, currentGems, amount);

            Debug.Log($"获得 {amount} 宝石 ({source})");
        }

        /// <summary>
        /// 消费宝石
        /// </summary>
        public bool SpendGems(int amount, string reason = "")
        {
            if (amount <= 0) return true;
            if (currentGems < amount) return false;

            currentGems -= amount;

            OnGemsChanged?.Invoke(currentGems, -amount);
            OnResourceChanged?.Invoke(ResourceType.Gem, currentGems, -amount);

            Debug.Log($"消费 {amount} 宝石 ({reason})");
            return true;
        }

        /// <summary>
        /// 设置能量
        /// </summary>
        public void SetEnergy(int amount)
        {
            int oldEnergy = currentEnergy;
            currentEnergy = Mathf.Max(0, amount);

            int change = currentEnergy - oldEnergy;
            OnResourceChanged?.Invoke(ResourceType.Energy, currentEnergy, change);
        }

        /// <summary>
        /// 记录击杀
        /// </summary>
        public void RecordKill(EnemySystem.EnemyBase enemy)
        {
            enemiesKilled++;

            // 添加击杀奖励
            AddCoins(enemy.coinDropAmount, $"击杀 {enemy.enemyType}");
            AddExperience(enemy.experienceValue, $"击杀 {enemy.enemyType}");
        }

        #endregion

        #region 存档/读档

        /// <summary>
        /// 保存数据
        /// </summary>
        public void SaveData()
        {
            SaveSystem.SaveResourceData(this);
        }

        /// <summary>
        /// 加载数据
        /// </summary>
        public bool LoadData()
        {
            var data = SaveSystem.LoadResourceData();
            if (data == null) return false;

            currentCoins = data.coins;
            currentExperience = data.experience;
            currentGems = data.gems;

            return true;
        }

        /// <summary>
        /// 重置所有资源
        /// </summary>
        public void ResetResources()
        {
            currentCoins = initialCoins;
            currentExperience = initialExperience;
            currentGems = initialGems;
            currentEnergy = 0;

            OnCoinsChanged?.Invoke(currentCoins, 0);
            OnExperienceChanged?.Invoke(currentExperience, 0);
            OnGemsChanged?.Invoke(currentGems, 0);

            SaveData();
        }

        #endregion

        #region 获取存档数据

        public ResourceSaveData GetSaveData()
        {
            return new ResourceSaveData
            {
                coins = currentCoins,
                experience = currentExperience,
                gems = currentGems,
                totalCoinsEarned = totalCoinsEarned,
                totalCoinsSpent = totalCoinsSpent,
                enemiesKilled = enemiesKilled
            };
        }

        #endregion
    }

    /// <summary>
    /// 资源存档数据
    /// </summary>
    [Serializable]
    public class ResourceSaveData
    {
        public int coins;
        public int experience;
        public int gems;
        public int totalCoinsEarned;
        public int totalCoinsSpent;
        public int enemiesKilled;
    }
}
