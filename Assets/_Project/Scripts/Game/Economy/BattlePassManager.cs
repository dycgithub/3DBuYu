using System;
using UnityEngine;
using Services;

namespace GameSystem
{
    /// <summary>
    /// 通行证存档。
    /// </summary>
    [Serializable]
    public class BattlePassSaveData
    {
        public int currentLevel = 1;
        public bool[] claimed;
    }

    /// <summary>
    /// 通行证管理器：单条轨道 100 级，每级消耗积分线性递增。
    /// 解锁：手动调 <see cref="TryUnlockNext(IPointsService)"/>，从 IPointsService 消费积分。
    /// 领奖：调 <see cref="ClaimReward(int)"/>，返回该级配置的奖励（itemId + amount）。
    /// </summary>
    public class BattlePassManager
    {
        public const int DefaultMaxLevel = 100;

        public int MaxLevel { get; private set; } = DefaultMaxLevel;
        public int CurrentLevel { get; private set; } = 1;
        public bool IsMaxLevel => CurrentLevel >= MaxLevel;

        /// <summary>升到新等级（仅在升级时触发）。</summary>
        public event Action<int> OnLevelUp;

        /// <summary>奖励被领取（level, 奖励数组）。</summary>
        public event Action<int, BattlePassRewardEntry[]> OnRewardClaimed;

        private BattlePassConfig config;

        public void Initialize(BattlePassConfig battlePassConfig)
        {
            config = battlePassConfig;
            MaxLevel = config != null ? Mathf.Max(1, config.totalLevels) : DefaultMaxLevel;
            CurrentLevel = 1;
        }

        public void LoadFromSave(BattlePassSaveData data)
        {
            if (data == null) return;
            CurrentLevel = Mathf.Clamp(data.currentLevel, 1, MaxLevel);
        }

        /// <summary>
        /// 当前等级升一级所需积分（无配置时返回 int.MaxValue）。
        /// </summary>
        public int GetCostToNext()
        {
            if (config == null) return int.MaxValue;
            return config.GetCostForNextStep(CurrentLevel);
        }

        /// <summary>
        /// 是否还有下一级可升。
        /// </summary>
        public bool CanUnlockNext()
        {
            if (IsMaxLevel || config == null) return false;
            return true;
        }

        /// <summary>
        /// 尝试升一级。成功扣分并触发 <see cref="OnLevelUp"/>；积分不足或已满级返回 false。
        /// </summary>
        public bool TryUnlockNext(IPointsService points)
        {
            if (!CanUnlockNext() || points == null) return false;
            int cost = GetCostToNext();
            if (cost <= 0 || !points.HasEnoughPoints(cost)) return false;

            if (!points.SpendPoints(cost, $"BP Lv{CurrentLevel}→Lv{CurrentLevel + 1}"))
                return false;

            CurrentLevel++;
            OnLevelUp?.Invoke(CurrentLevel);
            return true;
        }

        /// <summary>
        /// 是否可以领取指定等级奖励（必须先解锁到该级，且未领取过）。
        /// </summary>
        public bool CanClaim(int level)
        {
            if (config == null) return false;
            if (level < 1 || level > CurrentLevel) return false;
            if (level > MaxLevel) return false;
            var claimed = GetClaimed();
            return !claimed[level - 1];
        }

        /// <summary>
        /// 领取指定等级奖励。返回该级配置的奖励（itemId + amount），不可领取则返回空数组。
        /// </summary>
        public BattlePassRewardEntry[] ClaimReward(int level)
        {
            if (!CanClaim(level)) return Array.Empty<BattlePassRewardEntry>();
            var claimed = GetClaimed();
            claimed[level - 1] = true;
            var rewards = config.GetRewardsFor(level);
            OnRewardClaimed?.Invoke(level, rewards);
            return rewards;
        }

        /// <summary>
        /// 获取存档数据。
        /// </summary>
        public BattlePassSaveData GetSaveData()
        {
            return new BattlePassSaveData
            {
                currentLevel = CurrentLevel,
                claimed = GetClaimed()
            };
        }

        /// <summary>
        /// 内部 claimed[] 懒分配，保证长度匹配。
        /// </summary>
        private bool[] GetClaimed()
        {
            if (_claimed == null || _claimed.Length != MaxLevel)
            {
                var arr = new bool[MaxLevel];
                if (_claimed != null)
                {
                    int copy = Mathf.Min(_claimed.Length, arr.Length);
                    for (int i = 0; i < copy; i++) arr[i] = _claimed[i];
                }
                _claimed = arr;
            }
            return _claimed;
        }

        private bool[] _claimed;
    }
}
