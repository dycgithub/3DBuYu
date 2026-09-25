using System;

namespace Supply
{
    /// <summary>将强度分数映射为统一的等级和效果倍率。</summary>
    public readonly struct PowerLevelValue
    {
        /// <summary>强度等级。</summary>
        public int Level { get; }

        /// <summary>对应效果倍率。</summary>
        public float Scale { get; }

        /// <summary>创建一个强度映射值。</summary>
        /// <param name="level">强度等级。</param>
        /// <param name="scale">效果倍率。</param>
        public PowerLevelValue(int level, float scale)
        {
            Level = level;
            Scale = scale;
        }
    }

    /// <summary>提供所有 Supply 物品共用的强度表。</summary>
    public static class ItemPowerTable
    {
        /// <summary>统一强度表的最大等级。</summary>
        public const int MaximumLevel = 8;

        private static readonly int[] MinimumScores = { 1, 17, 33, 49, 65, 81, 97, 113 };
        private static readonly float[] Scales = { 0.5f, 0.65f, 0.8f, 1f, 1.25f, 1.5f, 1.8f, 2.2f };

        /// <summary>将宝箱强度分数转换为等级和倍率。</summary>
        /// <param name="score">强度分数。</param>
        /// <returns>统一强度表中的映射值。</returns>
        public static PowerLevelValue Resolve(int score)
        {
            if (score <= 0)
            {
                return new PowerLevelValue(0, 1f);
            }

            for (int index = MinimumScores.Length - 1; index >= 0; index--)
            {
                if (score >= MinimumScores[index])
                {
                    return new PowerLevelValue(index + 1, Scales[index]);
                }
            }

            return new PowerLevelValue(0, 1f);
        }

        /// <summary>按物品强度等级查询统一效果倍率，供战斗链路使用。</summary>
        /// <param name="powerLevel">物品强度等级。</param>
        /// <returns>对应等级的效果倍率；无强度等级使用 1 倍。</returns>
        public static float GetScale(int powerLevel)
        {
            int normalizedLevel = NormalizeLevel(powerLevel);
            if (normalizedLevel == 0)
            {
                return 1f;
            }

            int index = normalizedLevel - 1;
            return Scales[index];
        }

        /// <summary>将实例强度限制在统一表的有效范围内。</summary>
        /// <param name="powerLevel">待规范化等级。</param>
        /// <returns>0 到最大等级之间的等级。</returns>
        public static int NormalizeLevel(int powerLevel)
        {
            return Math.Max(0, Math.Min(MaximumLevel, powerLevel));
        }
    }
}
