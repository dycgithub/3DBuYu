using System;
using System.Collections.Generic;

namespace Supply
{
    /// <summary>提供固定 2×2×2 方块的共享面邻接关系。</summary>
    public static class CubeTopology
    {
        /// <summary>宝箱中的方块总数。</summary>
        public const int CubeCount = 8;

        /// <summary>返回指定方块的共享面邻居，不包含棱接触和角接触。</summary>
        /// <param name="cubeIndex">方块索引，按 x + 2y + 4z 编码。</param>
        /// <returns>共享面邻居索引。</returns>
        public static IEnumerable<int> GetNeighbors(int cubeIndex)
        {
            if (cubeIndex < 0 || cubeIndex >= CubeCount)
            {
                throw new ArgumentOutOfRangeException(nameof(cubeIndex));
            }

            int x = cubeIndex & 1;
            int y = (cubeIndex >> 1) & 1;
            int z = (cubeIndex >> 2) & 1;

            if (x == 0)
            {
                yield return cubeIndex + 1;
            }
            else
            {
                yield return cubeIndex - 1;
            }

            if (y == 0)
            {
                yield return cubeIndex + 2;
            }
            else
            {
                yield return cubeIndex - 2;
            }

            if (z == 0)
            {
                yield return cubeIndex + 4;
            }
            else
            {
                yield return cubeIndex - 4;
            }
        }
    }

    /// <summary>保存单个宝箱方块的领域状态。</summary>
    public sealed class ChestCubeState
    {
        /// <summary>方块索引。</summary>
        public int Index { get; }

        /// <summary>当前颜色等级，范围为 1 到 10。</summary>
        public int ColorLevel { get; private set; }

        /// <summary>方块是否已经碎裂。</summary>
        public bool IsBroken { get; private set; }

        /// <summary>创建一个宝箱方块状态。</summary>
        /// <param name="index">方块索引。</param>
        /// <param name="colorLevel">初始颜色等级。</param>
        public ChestCubeState(int index, int colorLevel)
        {
            Index = index;
            ColorLevel = ClampColor(colorLevel);
        }

        /// <summary>将方块标记为碎裂。</summary>
        internal void Break()
        {
            IsBroken = true;
        }

        /// <summary>设置方块颜色等级。</summary>
        /// <param name="colorLevel">新的颜色等级。</param>
        internal void SetColor(int colorLevel)
        {
            ColorLevel = ClampColor(colorLevel);
        }

        /// <summary>将颜色等级限制在宝箱规则范围内。</summary>
        /// <param name="colorLevel">待限制等级。</param>
        /// <returns>有效颜色等级。</returns>
        private static int ClampColor(int colorLevel)
        {
            return Math.Max(1, Math.Min(10, colorLevel));
        }
    }

    /// <summary>集中保存宝箱的费用、点击和结算规则。</summary>
    public sealed class ChestRules
    {
        /// <summary>开始宝箱交互的费用。</summary>
        public int OpeningCost => 150;

        /// <summary>前五次事件至少保留的未碎裂方块数。</summary>
        public int MinimumRemainingCubes => 1;

        /// <summary>第五次事件后执行提前结算检查所使用的概率。</summary>
        public float SettleChance { get; }

        /// <summary>使用指定的提前结算概率创建宝箱规则。</summary>
        /// <param name="settleChance">第五次事件后的提前结算概率。</param>
        private ChestRules(float settleChance)
        {
            SettleChance = IsFinite(settleChance)
                ? Math.Max(0f, Math.Min(1f, settleChance))
                : 0f;
        }

        /// <summary>创建用于规则测试的宝箱规则。</summary>
        /// <param name="settleChance">第五次事件后的提前结算概率。</param>
        /// <returns>新的宝箱规则。</returns>
        public static ChestRules CreateTestRules(float settleChance)
        {
            return new ChestRules(settleChance);
        }

        /// <summary>创建游戏使用的默认宝箱规则。</summary>
        /// <returns>第五次点击后有四分之一提前结算概率的规则。</returns>
        public static ChestRules CreateDefault()
        {
            return new ChestRules(0.25f);
        }

        /// <summary>判断浮点值是否为有限数。</summary>
        /// <param name="value">待检查的值。</param>
        /// <returns>不是 NaN 或无穷大时返回 true。</returns>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }

    /// <summary>记录宝箱结算时的数量、强度和奖励。</summary>
    public sealed class ChestSettlement
    {
        /// <summary>未碎裂方块对应的奖励数量。</summary>
        public int ItemCount { get; }

        /// <summary>未碎裂方块颜色分数之和。</summary>
        public int StrengthScore { get; }

        /// <summary>由统一强度表解析出的奖励等级。</summary>
        public int PowerLevel { get; }

        /// <summary>结算生成的奖励。</summary>
        public IReadOnlyList<SupplyRewardItem> Rewards { get; }

        /// <summary>创建宝箱结算结果。</summary>
        /// <param name="itemCount">奖励数量。</param>
        /// <param name="strengthScore">强度分数。</param>
        /// <param name="powerLevel">强度等级。</param>
        /// <param name="rewards">奖励列表。</param>
        public ChestSettlement(
            int itemCount,
            int strengthScore,
            int powerLevel,
            IReadOnlyList<SupplyRewardItem> rewards)
        {
            ItemCount = itemCount;
            StrengthScore = strengthScore;
            PowerLevel = powerLevel;
            Rewards = rewards ?? Array.Empty<SupplyRewardItem>();
        }
    }

    /// <summary>记录一次宝箱点击是否被接受以及是否产生结算。</summary>
    public sealed class ChestActionResult
    {
        /// <summary>点击通过当前状态校验时为 true。</summary>
        public bool Accepted { get; }

        /// <summary>本次点击完成结算时为 true。</summary>
        public bool IsSettled { get; }

        /// <summary>完成结算时的结果，否则为 null。</summary>
        public ChestSettlement Settlement { get; }

        /// <summary>创建宝箱动作结果。</summary>
        /// <param name="accepted">动作是否被接受。</param>
        /// <param name="isSettled">是否完成结算。</param>
        /// <param name="settlement">结算结果。</param>
        public ChestActionResult(bool accepted, bool isSettled, ChestSettlement settlement)
        {
            Accepted = accepted;
            IsSettled = isSettled;
            Settlement = settlement;
        }
    }
}
