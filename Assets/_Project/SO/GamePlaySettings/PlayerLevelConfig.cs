using UnityEngine;

namespace GameSystem
{
    /// <summary>
    /// 玩家等级曲线配置。
    /// 等级 = floor(basePoints * level^1.5) 的累计积分阈值。
    /// 等级 N 需要的总累计积分 = GetCumulativePoints(N)。
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerLevelConfig", menuName = "Game/Player Level Config")]
    public class PlayerLevelConfig : ScriptableObject
    {
        [Tooltip("曲线基数。累计积分 = floor(basePoints * level^1.5)")]
        public int basePoints = 80;

        [Tooltip("软最大等级。超过此等级仍可累计积分，但等级不再上升。")]
        public int maxLevel = 100;

        /// <summary>
        /// 达到指定等级所需的累计积分。
        /// </summary>
        public int GetCumulativePoints(int level)
        {
            if (level <= 0) return 0;
            double v = basePoints * System.Math.Pow(level, 1.5);
            return (int)System.Math.Floor(v);
        }

        /// <summary>
        /// 根据累计积分反查当前等级(level ≤ maxLevel)。
        /// </summary>
        public int GetLevelFromPoints(int totalPoints)
        {
            if (totalPoints <= 0) return 0;
            for (int lv = maxLevel; lv >= 1; lv--)
            {
                if (totalPoints >= GetCumulativePoints(lv))
                    return lv;
            }
            return 0;
        }
    }
}
