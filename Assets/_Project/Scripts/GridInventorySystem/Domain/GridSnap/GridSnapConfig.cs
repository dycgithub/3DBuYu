using System;

namespace InventorySystem.GridSnap
{
    /// <summary>
    /// 吸附网格参数:步长 + 网格尺寸。
    /// 所有网格(仓库/炮塔/端口/商店)共用同一配置来源,保证大小与排列一致。
    /// </summary>
    public readonly struct GridSnapConfig
    {
        /// <summary>格子步长 = cellSize + spacing。</summary>
        public readonly float Step;

        /// <summary>网格列数。</summary>
        public readonly int Columns;

        /// <summary>网格行数。</summary>
        public readonly int Rows;

        public GridSnapConfig(float step, int columns, int rows)
        {
            Step = step;
            Columns = columns;
            Rows = rows;
        }

        /// <summary>将格子坐标限制在网格边界内。</summary>
        public SnapCell Clamp(SnapCell cell) => new SnapCell(
            Math.Clamp(cell.Row, 0, Rows - 1),
            Math.Clamp(cell.Col, 0, Columns - 1));
    }
}
