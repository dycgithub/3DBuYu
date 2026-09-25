using System;
using System.Collections.Generic;
using Services;

namespace Supply
{
    /// <summary>记录一次老虎机抽轴结果。</summary>
    public sealed class SlotMachineResult
    {
        /// <summary>所有轴的物品标识相同时为 true。</summary>
        public bool IsWin { get; }

        /// <summary>每条轴已经确定的物品标识。</summary>
        public IReadOnlyList<string> AxisResults { get; }

        /// <summary>本次中奖奖励；未中奖时为空集合。</summary>
        public IReadOnlyList<SupplyRewardItem> Rewards { get; }

        /// <summary>创建老虎机结果。</summary>
        /// <param name="isWin">是否中奖。</param>
        /// <param name="axisResults">每条轴的最终物品标识。</param>
        /// <param name="rewards">奖励列表。</param>
        public SlotMachineResult(
            bool isWin,
            IReadOnlyList<string> axisResults,
            IReadOnlyList<SupplyRewardItem> rewards)
        {
            IsWin = isWin;
            AxisResults = axisResults ?? Array.Empty<string>();
            Rewards = rewards ?? Array.Empty<SupplyRewardItem>();
        }
    }

    /// <summary>执行独立抽轴、扣费和轴数升级。</summary>
    public sealed class SlotMachineService
    {
        private const int InitialReelCount = 3;
        private const int MaximumReelCount = 9;
        private const int SpinBaseCost = 100;
        private const int UpgradeBaseCost = 150;
        private const int SpinCostStep = 25;
        private const int UpgradeCostStep = 50;

        private readonly IPointsService _points;
        private readonly SupplyItemPool _pool;
        private readonly IRandomSource _random;
        private bool _isBusy;

        /// <summary>当前独立抽取轴数。</summary>
        public int ReelCount { get; private set; } = InitialReelCount;

        /// <summary>创建老虎机服务。</summary>
        /// <param name="points">积分服务。</param>
        /// <param name="pool">可抽取物品池。</param>
        /// <param name="random">可重复随机源。</param>
        public SlotMachineService(IPointsService points, SupplyItemPool pool, IRandomSource random)
        {
            _points = points ?? throw new ArgumentNullException(nameof(points));
            _pool = pool ?? throw new ArgumentNullException(nameof(pool));
            _random = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>扣除当前轴数费用并独立抽取每一条轴。</summary>
        /// <param name="result">扣费并抽取成功后的结果。</param>
        /// <returns>完成抽取时返回 true；积分或物品池不足时返回 false。</returns>
        public bool TrySpin(out SlotMachineResult result)
        {
            result = null;
            if (_isBusy)
            {
                return false;
            }

            _isBusy = true;
            try
            {
                if (!HasEntries() || !TrySpend(SpinCost()))
                {
                    return false;
                }

                var itemIds = new string[ReelCount];
                bool isWin = true;
                for (int index = 0; index < ReelCount; index++)
                {
                    _pool.TryDraw(_random, SupplyItemRole.All, out SupplyItemEntry entry);
                    itemIds[index] = entry.ItemId;
                    if (index > 0 && itemIds[index] != itemIds[0])
                    {
                        isWin = false;
                    }
                }

                var rewards = new List<SupplyRewardItem>();
                if (isWin)
                {
                    for (int index = 0; index < ReelCount; index++)
                    {
                        rewards.Add(new SupplyRewardItem(itemIds[0], 0));
                    }
                }

                result = new SlotMachineResult(
                    isWin,
                    Array.AsReadOnly(itemIds),
                    rewards.AsReadOnly());
                return true;
            }
            finally
            {
                _isBusy = false;
            }
        }

        /// <summary>支付升级费用并增加一条抽取轴。</summary>
        /// <returns>升级成功时返回 true。</returns>
        public bool TryUpgrade()
        {
            if (_isBusy)
            {
                return false;
            }

            _isBusy = true;
            try
            {
                if (ReelCount >= MaximumReelCount || !TrySpend(UpgradeCost()))
                {
                    return false;
                }

                ReelCount++;
                return true;
            }
            finally
            {
                _isBusy = false;
            }
        }

        /// <summary>判断当前池是否存在可抽取的有效条目。</summary>
        /// <returns>存在有效物品时返回 true。</returns>
        private bool HasEntries()
        {
            return _pool.HasEntries(SupplyItemRole.All);
        }

        /// <summary>以统一原因从积分服务扣除指定费用。</summary>
        /// <param name="cost">费用。</param>
        /// <returns>扣费成功时返回 true。</returns>
        private bool TrySpend(int cost)
        {
            return _points.HasEnoughPoints(cost) && _points.SpendPoints(cost, "Supply");
        }

        /// <summary>计算当前轴数的抽取费用。</summary>
        /// <returns>抽取费用。</returns>
        private int SpinCost()
        {
            return SpinBaseCost + (ReelCount - InitialReelCount) * SpinCostStep;
        }

        /// <summary>计算当前轴数的升级费用。</summary>
        /// <returns>升级费用。</returns>
        private int UpgradeCost()
        {
            return UpgradeBaseCost + (ReelCount - InitialReelCount) * UpgradeCostStep;
        }
    }
}
