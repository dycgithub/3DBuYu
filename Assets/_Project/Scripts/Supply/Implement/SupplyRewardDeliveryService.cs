using System;
using System.Collections.Generic;
using Services;

namespace Supply
{
    /// <summary>描述奖励接收端对一项奖励的处理结果。</summary>
    public enum RewardPlacementStatus
    {
        /// <summary>奖励已放入存储。</summary>
        Placed,

        /// <summary>存储已满，奖励应回收为积分。</summary>
        Full,

        /// <summary>存储当前不可用，奖励不进行回收。</summary>
        Unavailable
    }

    /// <summary>接收 Supply 奖励，不搜索场景或操作表现层。</summary>
    public interface IStorageRewardSink
    {
        /// <summary>尝试放置一项奖励。</summary>
        /// <param name="reward">待放置奖励。</param>
        /// <returns>存储处理状态。</returns>
        RewardPlacementStatus TryPlace(SupplyRewardItem reward);
    }

    /// <summary>记录奖励交付的放置、回收和不可用数量。</summary>
    public class SupplyRewardResult
    {
        /// <summary>成功放置的奖励数量。</summary>
        public int PlacedCount { get; }

        /// <summary>因存储已满而回收的奖励数量。</summary>
        public int RecycledCount { get; }

        /// <summary>因存储不可用而未处理的奖励数量。</summary>
        public int UnavailableCount { get; }

        /// <summary>因存储不可用而保留、可稍后重试的奖励。</summary>
        public IReadOnlyList<SupplyRewardItem> UnavailableRewards { get; }

        /// <summary>回收得到的积分。</summary>
        public int RecycledPoints { get; }

        /// <summary>创建奖励交付结果。</summary>
        /// <param name="placedCount">放置数量。</param>
        /// <param name="recycledCount">回收数量。</param>
        /// <param name="unavailableCount">不可用数量。</param>
        /// <param name="recycledPoints">回收积分。</param>
        /// <param name="unavailableRewards">未交付奖励。</param>
        public SupplyRewardResult(
            int placedCount,
            int recycledCount,
            int unavailableCount,
            int recycledPoints,
            IReadOnlyList<SupplyRewardItem> unavailableRewards)
        {
            PlacedCount = placedCount;
            RecycledCount = recycledCount;
            UnavailableCount = unavailableCount;
            RecycledPoints = recycledPoints;
            UnavailableRewards = unavailableRewards ?? Array.Empty<SupplyRewardItem>();
        }
    }

    /// <summary>兼容测试公开名称的奖励交付结果。</summary>
    public sealed class SupplyRewardDeliveryResult : SupplyRewardResult
    {
        /// <summary>创建奖励交付结果。</summary>
        /// <param name="placedCount">放置数量。</param>
        /// <param name="recycledCount">回收数量。</param>
        /// <param name="unavailableCount">不可用数量。</param>
        /// <param name="recycledPoints">回收积分。</param>
        /// <param name="unavailableRewards">未交付奖励。</param>
        public SupplyRewardDeliveryResult(
            int placedCount,
            int recycledCount,
            int unavailableCount,
            int recycledPoints,
            IReadOnlyList<SupplyRewardItem> unavailableRewards)
            : base(
                placedCount,
                recycledCount,
                unavailableCount,
                recycledPoints,
                unavailableRewards)
        {
        }
    }

    /// <summary>将奖励放入存储，并将仅因容量不足的奖励回收为积分。</summary>
    public sealed class SupplyRewardDeliveryService
    {
        private readonly IStorageRewardSink _sink;
        private readonly IPointsService _points;
        private readonly Func<SupplyRewardItem, int> _recycleValue;
        private bool _isBusy;

        /// <summary>创建奖励交付服务。</summary>
        /// <param name="sink">奖励存储接收端。</param>
        /// <param name="points">积分服务。</param>
        /// <param name="recycleValue">计算单项奖励回收积分的函数。</param>
        public SupplyRewardDeliveryService(
            IStorageRewardSink sink,
            IPointsService points,
            Func<SupplyRewardItem, int> recycleValue)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _points = points ?? throw new ArgumentNullException(nameof(points));
            _recycleValue = recycleValue ?? throw new ArgumentNullException(nameof(recycleValue));
        }

        /// <summary>逐项交付奖励并结算容量不足的回收积分。</summary>
        /// <param name="rewards">待交付奖励。</param>
        /// <returns>本批奖励交付结果。</returns>
        public SupplyRewardDeliveryResult Deliver(IEnumerable<SupplyRewardItem> rewards)
        {
            if (_isBusy)
            {
                return new SupplyRewardDeliveryResult(
                    0,
                    0,
                    0,
                    0,
                    Array.Empty<SupplyRewardItem>());
            }

            _isBusy = true;
            try
            {
                return DeliverInternal(rewards);
            }
            finally
            {
                _isBusy = false;
            }
        }

        /// <summary>执行一批奖励的实际交付。</summary>
        /// <param name="rewards">待交付奖励。</param>
        /// <returns>本批奖励交付结果。</returns>
        private SupplyRewardDeliveryResult DeliverInternal(IEnumerable<SupplyRewardItem> rewards)
        {
            int placedCount = 0;
            int recycledCount = 0;
            int unavailableCount = 0;
            int recycledPoints = 0;
            var unavailableRewards = new List<SupplyRewardItem>();

            if (rewards != null)
            {
                foreach (SupplyRewardItem reward in rewards)
                {
                    if (reward == null)
                    {
                        continue;
                    }

                    RewardPlacementStatus status = _sink.TryPlace(reward);
                    switch (status)
                    {
                        case RewardPlacementStatus.Placed:
                            placedCount++;
                            break;
                        case RewardPlacementStatus.Full:
                            recycledCount++;
                            recycledPoints = AddPoints(recycledPoints, _recycleValue(reward));
                            break;
                        default:
                            unavailableCount++;
                            unavailableRewards.Add(reward);
                            break;
                    }
                }
            }

            if (recycledPoints > 0)
            {
                _points.AddPoints(recycledPoints, "Supply 奖励回收");
            }

            return new SupplyRewardDeliveryResult(
                placedCount,
                recycledCount,
                unavailableCount,
                recycledPoints,
                unavailableRewards.AsReadOnly());
        }

        /// <summary>安全累加回收积分并忽略负数回收值。</summary>
        /// <param name="current">当前回收积分。</param>
        /// <param name="value">待加入积分。</param>
        /// <returns>限制在整数范围内的回收积分。</returns>
        private static int AddPoints(int current, int value)
        {
            if (value <= 0 || value > int.MaxValue - current)
            {
                return value > 0 ? int.MaxValue : current;
            }

            return current + value;
        }
    }
}
