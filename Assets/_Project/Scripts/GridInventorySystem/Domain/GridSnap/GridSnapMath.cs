using System;
using System.Collections.Generic;

namespace InventorySystem.GridSnap
{
    /// <summary>
    /// 吸附纯数学:坐标换算、锚定偏移、形状适配判定。
    /// 零 Unity 依赖,可独立单元测试。
    /// </summary>
    public static class GridSnapMath
    {
        /// <summary>格子左上角 → 容器本地坐标(视觉坐标,Y 向下为正)。</summary>
        public static SnapPoint CellToLocal(SnapCell cell, GridSnapConfig config)
            => new SnapPoint(cell.Col * config.Step, cell.Row * config.Step);

        /// <summary>容器本地坐标 → 最近格子(按格子中心四舍五入,clamp 到边界)。</summary>
        public static SnapCell LocalToCell(SnapPoint local, GridSnapConfig config)
        {
            int col = (int)Math.Round(local.X / config.Step - 0.5f);
            int row = (int)Math.Round(local.Y / config.Step - 0.5f);
            return config.Clamp(new SnapCell(row, col));
        }

        /// <summary>
        /// 吸附锚定格 = 鼠标所在格 − 按下时鼠标在物品内的格偏移。
        /// 保证"鼠标始终指向物品同一格",视觉与落点严格一致。
        /// </summary>
        public static SnapCell ComputeAnchor(SnapCell hoverCell, SnapCell pointerOffset)
            => new SnapCell(hoverCell.Row - pointerOffset.Row, hoverCell.Col - pointerOffset.Col);

        /// <summary>形状包围盒(格数):宽 = maxC−minC+1,高 = maxR−minR+1。</summary>
        public static (int Width, int Height) GetBoundsInCells(IReadOnlyList<SnapCell> cells)
        {
            int minR = int.MaxValue, maxR = int.MinValue;
            int minC = int.MaxValue, maxC = int.MinValue;
            foreach (var c in cells)
            {
                minR = Math.Min(minR, c.Row); maxR = Math.Max(maxR, c.Row);
                minC = Math.Min(minC, c.Col); maxC = Math.Max(maxC, c.Col);
            }
            return (maxC - minC + 1, maxR - minR + 1);
        }

        /// <summary>形状能否在 anchor 处放置(委托注入的放置策略判定)。</summary>
        public static bool CanPlace(IReadOnlyList<SnapCell> cells, SnapCell anchor, IGridSnapPlacement placement)
        {
            if (placement == null || cells == null || cells.Count == 0) return false;
            return placement.CanPlaceAt(cells, anchor);
        }
    }
}
