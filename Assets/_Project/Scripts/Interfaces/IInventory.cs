using System;
using System.Collections.Generic;
using InventorySystem;
using ItemSystem;

namespace Interfaces
{
    /// <summary>
    /// 通用背包接口。
    /// TurretInventory、PortInventory、StorageInventory 共享此接口。
    /// </summary>
    public interface IInventory
    {
        /// <summary>底层网格数据。</summary>
        InventoryGrid Grid { get; }

        /// <summary>属性聚合器（装备属性计算）。</summary>
        IAttributesAggregator Attributes { get; }

        /// <summary>物品放置时触发。</summary>
        event Action<PlacedItem> OnItemPlaced;

        /// <summary>物品移除时触发。</summary>
        event Action<int> OnItemRemoved;

        /// <summary>背包整体变化时触发。</summary>
        event Action OnInventoryChanged;

        /// <summary>该库存是否接受此物品（类型规则校验）。</summary>
        bool CanAccept(ItemConfig config);

        /// <summary>物品（含旋转）能否放置在指定位置（类型规则 + 形状/边界/占用）。</summary>
        bool CanPlaceAt(ItemConfig config, int row, int col, int rotation);

        /// <summary>尝试放置物品到网格（无旋转）。</summary>
        int PlaceItem(ItemConfig config, int row, int col);

        /// <summary>尝试放置物品到网格（支持旋转）。</summary>
        int PlaceItem(ItemConfig config, int row, int col, int rotation);

        /// <summary>自动寻找空位放置物品。</summary>
        int AutoPlaceItem(ItemConfig config);

        /// <summary>移除物品。</summary>
        bool RemoveItem(int instanceId);

        /// <summary>移动物品（保持当前旋转）。</summary>
        bool MoveItem(int instanceId, int newRow, int newCol);

        /// <summary>移动物品（指定目标旋转）。</summary>
        bool MoveItem(int instanceId, int newRow, int newCol, int rotation);

        /// <summary>跨库存转移：在目标库存指定位置放置，成功则从本库存移除。</summary>
        bool TransferTo(IInventory target, int instanceId, int row, int col, int rotation);

        /// <summary>通知库存发生变化（属性重算 + 事件广播）。</summary>
        void NotifyChanged();

        /// <summary>获取所有已放置物品。</summary>
        IReadOnlyList<PlacedItem> GetAllItems();

        /// <summary>获取指定实例 ID 的物品配置。</summary>
        ItemConfig GetItemConfig(int instanceId);

        /// <summary>是否有空位放置给定物品。</summary>
        bool HasFreeSlot(ItemShape shape, ItemConfig config = null);
    }

}