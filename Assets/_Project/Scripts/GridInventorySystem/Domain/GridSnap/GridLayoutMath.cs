using UnityEngine;

namespace InventorySystem.GridSnap
{
    /// <summary>
    /// 网格布局便捷数学(视图友好):返回 UnityEngine.Vector2,供格子/物品/幽灵直接使用。
    /// 约定:容器左上角为原点,row 向下为正;与 GridSnapMath 的 SnapPoint 语义一致。
    /// </summary>
    public static class GridLayoutMath
    {
        /// <summary>格子 (row,col) 左上角相对容器左上角的本地坐标。</summary>
        public static Vector2 CellLocalPos(int row, int col, float step)
            => new Vector2(col * step, -row * step);

        /// <summary>物品包围盒尺寸:rows×cols 的格子区域(含间距)。</summary>
        public static Vector2 ShapeSize(int rows, int cols, float cellSize, float spacing)
            => new Vector2(
                cols * cellSize + (cols - 1) * spacing,
                rows * cellSize + (rows - 1) * spacing);
    }
}
