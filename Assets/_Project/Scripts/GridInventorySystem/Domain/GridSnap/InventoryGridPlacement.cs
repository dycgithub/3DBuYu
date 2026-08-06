using System.Collections.Generic;
using ItemSystem;

namespace InventorySystem.GridSnap
{
    /// <summary>
    /// 背包网格 → 通用吸附接口的适配器。
    /// 将 InventoryGrid 的放置判定(含旋转、排除自身)暴露为 IGridSnapPlacement。
    /// </summary>
    public sealed class InventoryGridPlacement : IGridSnapPlacement
    {
        private readonly InventoryGrid _grid;
        private readonly ItemConfig _config;
        private readonly int _rotation;
        private readonly int _excludeInstanceId;

        public InventoryGridPlacement(InventoryGrid grid, ItemConfig config, int rotation, int excludeInstanceId = -1)
        {
            _grid = grid;
            _config = config;
            _rotation = rotation;
            _excludeInstanceId = excludeInstanceId;
        }

        public bool CanPlaceAt(IReadOnlyList<SnapCell> cells, SnapCell anchor)
            => _grid.CanPlaceAt(_config, anchor.Row, anchor.Col, _rotation, _excludeInstanceId);
    }
}
