using System;
using System.Collections.Generic;
using Interfaces;
using ItemSystem;

namespace InventorySystem
{
    /// <summary>
    /// 背包基类 — 提供所有背包共用的 CRUD + 转移逻辑。
    /// 物品类型规则由 <see cref="Validator"/> 策略决定（构造注入），
    /// 子类只需决定网格尺寸与属性聚合器，不再覆盖校验方法。
    /// </summary>
    public abstract class BaseInventory : IInventory
    {
        public InventoryGrid Grid { get; }
        public IAttributesAggregator Attributes { get; }

        /// <summary>物品类型校验策略（如 ItemTypeValidator / AnyItemValidator）。</summary>
        public IInventoryValidator Validator { get; }

        public event Action<PlacedItem> OnItemPlaced;
        public event Action<int> OnItemRemoved;
        public event Action OnInventoryChanged;

        protected BaseInventory(int columns, int rows, IInventoryValidator validator, IAttributesAggregator attributes)
        {
            Grid = new InventoryGrid(columns, rows);
            Validator = validator ?? throw new ArgumentNullException(nameof(validator));
            Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        }

        protected BaseInventory(InventoryGrid grid, IInventoryValidator validator, IAttributesAggregator attributes)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            Validator = validator ?? throw new ArgumentNullException(nameof(validator));
            Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        }

        /// <summary>物品成功放置后的回调。返回 true 表示物品应被消耗（从网格移除）。</summary>
        protected virtual bool TryConsumeItem(ItemConfig config, int instanceId) => false;

        /// <summary>
        /// 通知库存发生了变化（供外部触发，如拖拽购买后）。
        /// 内部 PlaceItem/RemoveItem 等方法已自动调用此方法。
        /// </summary>
        public void NotifyChanged()
        {
            Attributes?.Recalculate(Grid.GetAllItems(), Grid);
            OnInventoryChanged?.Invoke();
        }

        /// <summary>该库存是否接受此物品（由 Validator 策略决定）。</summary>
        public bool CanAccept(ItemConfig config) => config != null && Validator.CanAccept(config);

        /// <summary>物品（含旋转）能否放置在指定位置（类型规则 + 形状/边界/占用）。</summary>
        public bool CanPlaceAt(ItemConfig config, int row, int col, int rotation)
            => CanAccept(config) && Grid.CanPlaceAt(config, row, col, rotation);

        #region CRUD

        /// <summary>放置物品到网格（无旋转）。</summary>
        public virtual int PlaceItem(ItemConfig config, int row, int col)
        {
            return PlaceItem(config, row, col, 0);
        }

        /// <summary>放置物品到网格（支持旋转）。</summary>
        public virtual int PlaceItem(ItemConfig config, int row, int col, int rotation)
        {
            if (config == null || config.shape == null) return -1;
            if (!CanAccept(config)) return -1;

            int instanceId = Grid.PlaceItem(config, row, col, rotation);
            if (instanceId < 0) return -1;

            Attributes.Recalculate(Grid.GetAllItems(), Grid);

            if (TryConsumeItem(config, instanceId))
            {
                Grid.RemoveItem(instanceId);
                Attributes.Recalculate(Grid.GetAllItems(), Grid);
                return -1;
            }

            var placed = Grid.GetPlacedItem(instanceId);
            if (placed.HasValue) OnItemPlaced?.Invoke(placed.Value);
            OnInventoryChanged?.Invoke();
            return instanceId;
        }

        public int AutoPlaceItem(ItemConfig config)
        {
            if (config == null || config.shape == null) return -1;
            if (!CanAccept(config)) return -1;
            var (row, col) = Grid.FindFirstFit(config.shape);
            if (row < 0) return -1;
            return PlaceItem(config, row, col);
        }

        /// <summary>
        /// 随机位置放置物品（商店货架用）：随机起点 + 随机旋转重试，失败后 FindFirstFit 兜底。
        /// </summary>
        public int PlaceRandomly(ItemConfig config, int attempts = 30)
        {
            if (config == null || config.shape == null) return -1;
            if (!CanAccept(config)) return -1;

            for (int i = 0; i < attempts; i++)
            {
                int rotation = UnityEngine.Random.Range(0, 4);
                int row = UnityEngine.Random.Range(0, Grid.Height);
                int col = UnityEngine.Random.Range(0, Grid.Width);
                if (Grid.CanPlaceAt(config, row, col, rotation))
                    return PlaceItem(config, row, col, rotation);
            }

            var (fitRow, fitCol) = Grid.FindFirstFit(config.shape);
            return fitRow >= 0 ? PlaceItem(config, fitRow, fitCol) : -1;
        }

        public bool RemoveItem(int instanceId)
        {
            if (!Grid.RemoveItem(instanceId)) return false;
            Attributes.Recalculate(Grid.GetAllItems(), Grid);
            OnItemRemoved?.Invoke(instanceId);
            OnInventoryChanged?.Invoke();
            return true;
        }

        /// <summary>移动物品到新位置（保持当前旋转）。</summary>
        public bool MoveItem(int instanceId, int newRow, int newCol)
        {
            var placed = Grid.GetPlacedItem(instanceId);
            return MoveItem(instanceId, newRow, newCol, placed?.rotation ?? 0);
        }

        /// <summary>移动物品到新位置（指定目标旋转，可原地旋转）。</summary>
        public bool MoveItem(int instanceId, int newRow, int newCol, int rotation)
        {
            var placed = Grid.GetPlacedItem(instanceId);
            if (!placed.HasValue) return false;
            var config = Grid.GetItemConfig(instanceId);
            if (config?.shape == null) return false;

            // 原地且未旋转 → 直接视为成功（拖起又放下）。
            if (placed.Value.row == newRow && placed.Value.col == newCol && placed.Value.rotation == rotation)
                return true;

            var cells = InventoryGrid.GetRotatedCells(config.shape, rotation);
            if (!Grid.MoveItemRotated(instanceId, newRow, newCol, cells, rotation))
                return false;

            // 与 PlaceItem/RemoveItem 一致：移动也是库存变化，广播事件供 UI 刷新。
            NotifyChanged();
            return true;
        }

        #endregion

        #region Transfer

        /// <summary>
        /// 将物品从本库存转移到目标库存（在目标库存指定位置放置，支持旋转）。
        /// 先校验类型规则与目标位置，成功落子后移除源库存物品，失败回滚。
        /// </summary>
        public bool TransferTo(IInventory target, int instanceId, int row, int col, int rotation)
        {
            if (target == null) return false;
            var config = GetItemConfig(instanceId);
            if (config == null) return false;
            if (!target.CanAccept(config)) return false;
            if (!target.CanPlaceAt(config, row, col, rotation)) return false;

            int newId = target.PlaceItem(config, row, col, rotation);
            if (newId < 0) return false;

            if (!RemoveItem(instanceId))
            {
                target.RemoveItem(newId);   // 回滚
                return false;
            }

            return true;
        }

        #endregion

        #region Query

        public IReadOnlyList<PlacedItem> GetAllItems() => Grid.GetAllItems();
        public ItemConfig GetItemConfig(int instanceId) => Grid.GetItemConfig(instanceId);
        public bool HasFreeSlot(ItemShape shape, ItemConfig config = null) => Grid.HasFreeSlot(shape);

        #endregion
    }
}
