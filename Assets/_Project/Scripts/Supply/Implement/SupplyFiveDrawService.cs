using System;
using System.Collections.Generic;
using Services;

namespace Supply
{
    /// <summary>承接抽中的道具；背包没有可用格位时返回 false。</summary>
    public interface ISupplyDrawStorage
    {
        bool IsReady { get; }
        bool TryStore(ItemDefinition definition);
    }

    /// <summary>一次抽取的结算状态。</summary>
    public enum SupplyFiveDrawStatus
    {
        Completed,
        InsufficientItems,
        InsufficientPoints,
        StorageUnavailable,
        Busy
    }

    /// <summary>一件抽中道具及其背包交付结果。</summary>
    public sealed class SupplyFiveDrawItem
    {
        public ItemDefinition Definition { get; }
        public bool Stored { get; }

        public SupplyFiveDrawItem(ItemDefinition definition, bool stored)
        {
            Definition = definition;
            Stored = stored;
        }
    }

    /// <summary>一次五件抽取的结果。</summary>
    public sealed class SupplyFiveDrawResult
    {
        public SupplyFiveDrawStatus Status { get; }
        public IReadOnlyList<SupplyFiveDrawItem> Items { get; }
        public int StoredCount { get; }
        public int DroppedCount => Items.Count - StoredCount;

        public SupplyFiveDrawResult(
            SupplyFiveDrawStatus status,
            IReadOnlyList<SupplyFiveDrawItem> items,
            int storedCount)
        {
            Status = status;
            Items = items;
            StoredCount = storedCount;
        }
    }

    /// <summary>从完整物品目录等概率抽取五种不同道具，逐件交给背包。</summary>
    public sealed class SupplyFiveDrawService
    {
        public const int DrawCount = 5;
        private const string DrawReason = "Supply 五件抽取";

        private readonly ItemCatalog _catalog;
        private readonly IRandomSource _random;
        private readonly IPointsService _points;
        private bool _isDrawing;

        public SupplyFiveDrawService(ItemCatalog catalog, IRandomSource random, IPointsService points)
        {
            _catalog = catalog;
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _points = points ?? throw new ArgumentNullException(nameof(points));
        }

        /// <summary>检查抽取所需的背包、奖池和积分。</summary>
        /// <param name="cost">单次抽取费用。</param>
        /// <param name="storage">目标普通背包。</param>
        /// <returns>当前可以抽取时返回 true。</returns>
        public bool CanDraw(int cost, ISupplyDrawStorage storage)
        {
            return !_isDrawing &&
                   cost >= 0 &&
                   storage != null && storage.IsReady &&
                   CollectItems().Count >= DrawCount &&
                   _points.HasEnoughPoints(cost);
        }

        /// <summary>一次扣费并抽出五种不同道具；放不下的道具直接丢弃。</summary>
        /// <param name="cost">单次抽取费用。</param>
        /// <param name="storage">目标普通背包。</param>
        /// <returns>五件道具各自的交付结果或拒绝原因。</returns>
        public SupplyFiveDrawResult TryDraw(int cost, ISupplyDrawStorage storage)
        {
            if (_isDrawing)
            {
                return Empty(SupplyFiveDrawStatus.Busy);
            }

            if (storage == null || !storage.IsReady)
            {
                return Empty(SupplyFiveDrawStatus.StorageUnavailable);
            }

            List<ItemDefinition> items = CollectItems();
            if (items.Count < DrawCount)
            {
                return Empty(SupplyFiveDrawStatus.InsufficientItems);
            }

            if (cost < 0 || !_points.HasEnoughPoints(cost) ||
                !_points.SpendPoints(cost, DrawReason))
            {
                return Empty(SupplyFiveDrawStatus.InsufficientPoints);
            }

            _isDrawing = true;
            try
            {
                var results = new List<SupplyFiveDrawItem>(DrawCount);
                int storedCount = 0;
                for (int index = 0; index < DrawCount; index++)
                {
                    int selected = index + _random.NextInt(items.Count - index);
                    (items[index], items[selected]) = (items[selected], items[index]);

                    bool stored = storage.TryStore(items[index]);
                    results.Add(new SupplyFiveDrawItem(items[index], stored));
                    if (stored)
                    {
                        storedCount++;
                    }
                }

                return new SupplyFiveDrawResult(
                    SupplyFiveDrawStatus.Completed,
                    results.AsReadOnly(),
                    storedCount);
            }
            finally
            {
                _isDrawing = false;
            }
        }

        /// <summary>收集目录中 ID 有效且唯一的物品定义。</summary>
        /// <returns>可参与本次抽取的完整道具池。</returns>
        private List<ItemDefinition> CollectItems()
        {
            var items = new List<ItemDefinition>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (_catalog == null)
            {
                return items;
            }

            foreach (ItemDefinition definition in _catalog.Items)
            {
                if (definition != null && !string.IsNullOrWhiteSpace(definition.Id) &&
                    ids.Add(definition.Id))
                {
                    items.Add(definition);
                }
            }

            return items;
        }

        /// <summary>创建没有抽中道具的拒绝结果。</summary>
        /// <param name="status">拒绝原因。</param>
        /// <returns>空结果。</returns>
        private static SupplyFiveDrawResult Empty(SupplyFiveDrawStatus status)
        {
            return new SupplyFiveDrawResult(status, Array.Empty<SupplyFiveDrawItem>(), 0);
        }
    }
}
