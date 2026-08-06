using System;
using UnityEngine;

namespace GameSystem
{
    /// <summary>
    /// 通行证单级奖励占位符。
    /// itemId 留空表示该级无奖励；否则由外部系统根据 itemId 解释（库存、商店、皮肤等）。
    /// </summary>
    [Serializable]
    public class BattlePassRewardEntry
    {
        [Tooltip("奖励物品ID（通用占位符，留空表示该级无奖励）。")]
        public string itemId = string.Empty;

        [Tooltip("奖励数量。")]
        public int amount = 1;
    }

    /// <summary>
    /// 通行证配置：单条轨道 100 级，线性递增积分。
    /// 公式：从 LvN 升到 LvN+1 需 N * baseCost 分。
    /// 累计花费到 LvK (K>=1) = baseCost * (0+1+...+(K-1)) = baseCost * (K-1)*K/2。
    /// baseCost=100 时：Lv2 累计 100，Lv10 累计 4500，Lv100 累计 495000。
    /// </summary>
    [CreateAssetMenu(fileName = "BattlePassConfig", menuName = "Game/Battle Pass Config")]
    public class BattlePassConfig : ScriptableObject
    {
        [Header("公式参数")]
        [Tooltip("线性基数。升一级所需 = 当前等级 * baseCost。")]
        public int baseCost = 100;

        [Tooltip("总等级数（含 Lv1 起点）。默认 100。")]
        public int totalLevels = 100;

        [Header("每级奖励（先全部留空）")]
        [Tooltip("下标 0 对应 Lv1，长度不足或越界视作该级无奖励。")]
        public BattlePassRewardEntry[] rewards = new BattlePassRewardEntry[100];

        /// <summary>
        /// 从 Lv1 升到目标等级累计所需积分。
        /// </summary>
        public int GetCumulativeCost(int targetLevel)
        {
            if (targetLevel <= 1) return 0;
            int n = targetLevel - 1;
            return baseCost * n * (n + 1) / 2;
        }

        /// <summary>
        /// 从当前等级升到下一级所需积分。
        /// </summary>
        public int GetCostForNextStep(int currentLevel)
        {
            int lv = Mathf.Clamp(currentLevel, 1, totalLevels);
            if (lv >= totalLevels) return int.MaxValue;
            return lv * baseCost;
        }

        /// <summary>
        /// 获取指定等级的奖励（不可变快照，过滤空 itemId）。
        /// </summary>
        public BattlePassRewardEntry[] GetRewardsFor(int level)
        {
            if (rewards == null) return Array.Empty<BattlePassRewardEntry>();
            int idx = level - 1;
            if (idx < 0 || idx >= rewards.Length) return Array.Empty<BattlePassRewardEntry>();
            var list = new System.Collections.Generic.List<BattlePassRewardEntry>(2);
            var entry = rewards[idx];
            if (entry == null || string.IsNullOrEmpty(entry.itemId)) return Array.Empty<BattlePassRewardEntry>();
            list.Add(new BattlePassRewardEntry { itemId = entry.itemId, amount = Mathf.Max(1, entry.amount) });
            return list.ToArray();
        }
    }
}
