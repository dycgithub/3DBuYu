using InventorySystem;

namespace Interfaces
{
    /// <summary>拖拽放置结果。</summary>
    public enum PlacementResult
    {
        /// <summary>放置成功。</summary>
        Success,

        /// <summary>拖拽载荷无效（无物品）。</summary>
        EmptyPayload,

        /// <summary>目标库存为空。</summary>
        NoTarget,

        /// <summary>目标位置被占用或越界。</summary>
        ShapeBlocked,

        /// <summary>物品类型不允许放入目标库存。</summary>
        TypeNotAllowed,

        /// <summary>转移执行失败。</summary>
        TransferFailed
    }

    /// <summary>
    /// 统一放置裁决服务：处理"任意来源（背包类）→ 任意目标网格"的放置判断与执行。
    /// 每次放置都执行：载荷有效性 → 目标类型规则 → 旋转形状/边界/占用 → 路由（同网格移动 / 跨网格转移）。
    /// 跨网格转移为事务式：先落目标、再从源移除、失败回滚。
    /// 商店购买不在此服务内（走 ShopManager），UI 层分流。
    /// </summary>
    public interface IPlacementService
    {
        PlacementResult TryPlace(DragPayload payload, IInventory target, int row, int col);
    }
}
