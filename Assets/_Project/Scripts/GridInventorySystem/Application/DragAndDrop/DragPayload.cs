using System.Collections.Generic;
using Interfaces;
using ItemSystem;

namespace InventorySystem
{
    /// <summary>拖拽来源类型。</summary>
    public enum DragSourceType
    {
        Inventory,
        Shop,
    }

    /// <summary>
    /// 拖拽会话载荷：描述"拖的是什么、从哪来、当前旋转状态"。
    /// 由拖拽源在 OnBeginDrag 时创建，存放在 InventoryDragGhost.ActivePayload（集中式拖拽会话），
    /// 放置目标在 OnDrop 时读取并交给 IPlacementService 裁决。
    /// </summary>
    public class DragPayload
    {
        public DragSourceType SourceType;

        /// <summary>来源库存。</summary>
        public IInventory SourceInventory;

        /// <summary>SourceType == Inventory 时：来源网格中的实例 ID。</summary>
        public int InstanceId = -1;

        /// <summary>物品配置（商店与背包源都有）。</summary>
        public ItemConfig ItemConfig;

        /// <summary>当前旋转状态（0-3，顺时针 90° 步进）。</summary>
        public int Rotation;

        /// <summary>旋转后的占格列表（与 Rotation 一致，供幽灵显示与放置裁决）。</summary>
        public List<(int row, int col)> Cells;
    }
}
