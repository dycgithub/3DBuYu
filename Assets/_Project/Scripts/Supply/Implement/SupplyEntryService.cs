using System;
using Services;

namespace Supply
{
    /// <summary>扣除入场费，并在场景加载失败时退款。</summary>
    public sealed class SupplyEntryService
    {
        private const string EntryReason = "Supply 入场费";
        private const string RefundReason = "Supply 入场失败退款";

        private readonly IPointsService _points;
        private readonly SupplyEntryCostsSO _costs;
        private SupplyMethod _pendingMethod;
        private bool _hasPendingEntry;

        /// <summary>创建抽奖入口支付服务。</summary>
        /// <param name="points">全局积分服务。</param>
        /// <param name="costs">各入口的费用配置。</param>
        public SupplyEntryService(IPointsService points, SupplyEntryCostsSO costs)
        {
            _points = points ?? throw new ArgumentNullException(nameof(points));
            _costs = costs;
        }

        /// <summary>读取指定入口的费用，供入口按钮显示。</summary>
        /// <param name="method">抽奖入口。</param>
        /// <returns>入场费用。</returns>
        public int GetCost(SupplyMethod method)
        {
            if (!Enum.IsDefined(typeof(SupplyMethod), method))
            {
                throw new ArgumentOutOfRangeException(nameof(method), method, null);
            }

            return _costs != null
                ? _costs.GetCost(method)
                : SupplyEntryCostsSO.DefaultEntryCost;
        }

        /// <summary>扣除一次入场费并记录待确认的入口。</summary>
        /// <param name="method">请求进入的抽奖入口。</param>
        /// <returns>扣费成功且没有其他待处理入口时返回 true。</returns>
        public bool TryBeginEntry(SupplyMethod method)
        {
            if (_hasPendingEntry || !Enum.IsDefined(typeof(SupplyMethod), method))
            {
                return false;
            }

            if (!_points.SpendPoints(GetCost(method), EntryReason))
            {
                return false;
            }

            _pendingMethod = method;
            _hasPendingEntry = true;
            return true;
        }

        /// <summary>场景成功加载后确认费用，不再退款。</summary>
        /// <param name="method">已加载的抽奖入口。</param>
        /// <returns>匹配当前待处理入口时返回 true。</returns>
        public bool ConfirmEntry(SupplyMethod method)
        {
            if (!IsPending(method))
            {
                return false;
            }

            _hasPendingEntry = false;
            return true;
        }

        /// <summary>场景加载失败时退还待处理入场费。</summary>
        /// <param name="method">加载失败的抽奖入口。</param>
        /// <returns>匹配当前待处理入口并完成退款时返回 true。</returns>
        public bool CancelEntry(SupplyMethod method)
        {
            if (!IsPending(method))
            {
                return false;
            }

            _points.AddPoints(GetCost(method), RefundReason);
            _hasPendingEntry = false;
            return true;
        }

        /// <summary>判断入口是否对应当前待处理支付。</summary>
        /// <param name="method">待确认或退款的入口。</param>
        /// <returns>入口匹配时返回 true。</returns>
        private bool IsPending(SupplyMethod method)
        {
            return _hasPendingEntry && _pendingMethod == method;
        }
    }
}
