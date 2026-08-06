using Interfaces;
using ItemSystem;

namespace InventorySystem
{
    /// <summary>
    /// 统一放置裁决服务实现。
    /// 同网格移动：旋转感知 + 排除自身占用。
    /// 跨网格转移：目标类型规则 + 位置校验 → 落目标 → 删源 → 失败回滚。
    /// </summary>
    public class PlacementService : IPlacementService
    {
        public PlacementResult TryPlace(DragPayload payload, IInventory target, int row, int col)
        {
            if (payload == null || payload.ItemConfig == null)
                return PlacementResult.EmptyPayload;

            if (target == null || target.Grid == null)
                return PlacementResult.NoTarget;

            var config = payload.ItemConfig;
            int rotation = payload.Rotation;
            int instanceId = payload.InstanceId;

            // 1) 目标库存类型规则（TurretInventory 只收 Skill、PortInventory 只收 Ammunition）
            if (!target.CanAccept(config))
                return PlacementResult.TypeNotAllowed;

            // 2) 同一库存 → 网格内移动（可旋转，排除自身占用）
            if (payload.SourceInventory != null && ReferenceEquals(payload.SourceInventory, target))
            {
                if (!target.Grid.CanPlaceAt(config, row, col, rotation, instanceId))
                    return PlacementResult.ShapeBlocked;

                bool moved = target.MoveItem(instanceId, row, col, rotation);
                if (!moved)
                    return PlacementResult.TransferFailed;

                // MoveItem 内部已广播 OnInventoryChanged（属性重算 + UI 刷新）
                return PlacementResult.Success;
            }

            // 3) 跨库存转移：先落目标，再从源移除；失败回滚（事务式）
            if (payload.SourceInventory == null)
                return PlacementResult.TransferFailed;

            if (!target.CanPlaceAt(config, row, col, rotation))
                return PlacementResult.ShapeBlocked;

            int newId = target.PlaceItem(config, row, col, rotation);
            if (newId < 0)
                return PlacementResult.TransferFailed;

            if (!payload.SourceInventory.RemoveItem(instanceId))
            {
                target.RemoveItem(newId);   // 回滚
                return PlacementResult.TransferFailed;
            }

            return PlacementResult.Success;
        }
    }
}
