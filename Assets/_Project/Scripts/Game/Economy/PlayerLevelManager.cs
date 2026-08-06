using System;
using UnityEngine;

namespace GameSystem
{
    /// <summary>
    /// 玩家等级存档。
    /// </summary>
    [Serializable]
    public class PlayerLevelSaveData
    {
        public int lifetimePoints;
        public int currentLevel;
    }

    /// <summary>
    /// 玩家等级管理器。
    /// 等级与累计获得过的总积分（lifetimePoints）挂钩，多项式曲线：
    /// cumulative = floor(base * level^1.5)。
    /// lifetimePoints 只增不减（消费不影响等级）。
    /// </summary>
    public class PlayerLevelManager
    {
        public PlayerLevelConfig Config { get; private set; }

        /// <summary>累计获得过的总积分（只增不减）。</summary>
        public int LifetimePoints { get; private set; }

        /// <summary>当前等级（0 表示未达到 1 级阈值）。</summary>
        public int CurrentLevel { get; private set; }

        /// <summary>下一级所需的总累计积分。已满级时返回 int.MaxValue。</summary>
        public int NextLevelThreshold
        {
            get
            {
                if (Config == null) return int.MaxValue;
                if (CurrentLevel >= Config.maxLevel) return int.MaxValue;
                return Config.GetCumulativePoints(CurrentLevel + 1);
            }
        }

        /// <summary>升到新等级时触发（可能跨多级）。</summary>
        public event Action<int> OnLevelUp;

        /// <summary>累计积分变化时触发（lifetimePoints, currentLevel）。</summary>
        public event Action<int, int> OnProgressChanged;

        public void Initialize(PlayerLevelConfig config)
        {
            Config = config;
            LifetimePoints = 0;
            CurrentLevel = config != null ? config.GetLevelFromPoints(0) : 0;
        }

        public void LoadFromSave(PlayerLevelSaveData data)
        {
            if (data == null) return;
            LifetimePoints = Mathf.Max(0, data.lifetimePoints);
            CurrentLevel = Mathf.Max(0, data.currentLevel);
        }

        /// <summary>
        /// 累加 lifetimePoints 并按曲线升到对应等级。
        /// 单次可跨多级；返回本次新达到的等级（若未升级返回 CurrentLevel）。
        /// </summary>
        public int AddLifetimePoints(int amount)
        {
            if (amount <= 0) return CurrentLevel;
            LifetimePoints += amount;

            int newLevel = Config != null
                ? Config.GetLevelFromPoints(LifetimePoints)
                : CurrentLevel;
            if (newLevel > CurrentLevel)
            {
                CurrentLevel = newLevel;
                OnLevelUp?.Invoke(CurrentLevel);
            }

            OnProgressChanged?.Invoke(LifetimePoints, CurrentLevel);
            return CurrentLevel;
        }

        public PlayerLevelSaveData GetSaveData()
        {
            return new PlayerLevelSaveData
            {
                lifetimePoints = LifetimePoints,
                currentLevel = CurrentLevel
            };
        }
    }
}
