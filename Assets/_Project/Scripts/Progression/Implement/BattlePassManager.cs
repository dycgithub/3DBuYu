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

        private BattlePassConfig _battlePassConfig;

        /// <summary>
        /// 使用通行证配置初始化当前进度。
        /// </summary>
        /// <param name="battlePassConfig">通行证配置。</param>
        public void Initialize(BattlePassConfig battlePassConfig)
        {
            _battlePassConfig = battlePassConfig;
            MaxLevel = _battlePassConfig != null
                ? Mathf.Max(1, _battlePassConfig.totalLevels)
                : DefaultMaxLevel;
            CurrentLevel = 1;
        }

        /// <summary>
        /// 从存档恢复当前通行证等级。
        /// </summary>
        /// <param name="data">通行证存档数据。</param>
        public void LoadFromSave(BattlePassSaveData data)
        {
            if (data == null)
            {
                return;
            }

            CurrentLevel = Mathf.Clamp(data.currentLevel, 1, MaxLevel);
        }

        /// <summary>
        /// 当前等级升一级所需积分（无配置时返回 int.MaxValue）。
        /// </summary>
        public int GetCostToNext()
        {
            if (_battlePassConfig == null)
            {
                return int.MaxValue;
            }

            return _battlePassConfig.GetCostForNextStep(CurrentLevel);
        }

        /// <summary>
        /// 是否还有下一级可升。
        /// </summary>
        public bool CanUnlockNext()
        {
            if (IsMaxLevel || _battlePassConfig == null)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 尝试升一级。成功扣分并触发 <see cref="OnLevelUp"/>；积分不足或已满级返回 false。
        /// </summary>
        public bool TryUnlockNext(IPointsService points)
        {
            if (!CanUnlockNext() || points == null)
            {
                return false;
            }

            int cost = GetCostToNext();
            if (cost <= 0 || !points.HasEnoughPoints(cost))
            {
                return false;
            }

            if (!points.SpendPoints(cost, $"BP Lv{CurrentLevel}→Lv{CurrentLevel + 1}"))
            {
                return false;
            }

            CurrentLevel++;
            OnLevelUp?.Invoke(CurrentLevel);
            return true;
        }

        /// <summary>
        /// 是否可以领取指定等级奖励（必须先解锁到该级，且未领取过）。
        /// </summary>
        public bool CanClaim(int level)
        {
            if (_battlePassConfig == null)
            {
                return false;
            }

            if (level < 1 || level > CurrentLevel)
            {
                return false;
            }

            if (level > MaxLevel)
            {
                return false;
            }

            bool[] claimed = GetOrCreateClaimedLevels();
            return !claimed[level - 1];
        }

        /// <summary>
        /// 领取指定等级奖励。返回该级配置的奖励（itemId + amount），不可领取则返回空数组。
        /// </summary>
        public BattlePassRewardEntry[] ClaimReward(int level)
        {
            if (!CanClaim(level))
            {
                return Array.Empty<BattlePassRewardEntry>();
            }

            bool[] claimed = GetOrCreateClaimedLevels();
            claimed[level - 1] = true;
            BattlePassRewardEntry[] rewards = _battlePassConfig.GetRewardsFor(level);
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
                claimed = GetOrCreateClaimedLevels()
            };
        }

        /// <summary>
        /// 内部 claimed[] 懒分配，保证长度匹配。
        /// </summary>
        /// <summary>
        /// 获取或创建与当前通行证最大等级匹配的领取状态数组。
        /// </summary>
        /// <returns>当前等级领取状态数组。</returns>
        private bool[] GetOrCreateClaimedLevels()
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
