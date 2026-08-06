using UnityEngine;

public static class GridLayoutRule
{
    /// <summary>格子 (row,col) 左上角相对容器左上角的本地坐标。</summary>
    public static Vector2 CellLocalPos(int row, int col, float step)
        => new Vector2(col * step, -row * step);

    /// <summary>容器本地坐标 → 最近格子坐标(clamp 到网格内)。</summary>
    public static Vector2Int LocalToCell(Vector2 local, float step, Vector2Int gridSize)
    {
        int col = Mathf.RoundToInt(local.x / step - 0.5f);
        int row = Mathf.RoundToInt(-local.y / step - 0.5f);
        return Clamp(row, col, gridSize);
    }

    /// <summary>屏幕坐标 → 网格坐标(经容器 RectTransform 反算)。</summary>
    public static Vector2Int ScreenToCell(Vector2 screenPos, RectTransform container,
        Camera uiCamera, float step, Vector2Int gridSize)
    {
        if (container == null) return Vector2Int.zero;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            container, screenPos, uiCamera, out var local);
        return LocalToCell(local, step, gridSize);
    }

    /// <summary>物品包围盒尺寸:cols 列 × rows 行的格子区域(含间距)。</summary>
    public static Vector2 ShapeSize(int rows, int cols, float cellSize, float spacing)
        => new Vector2(cols * cellSize + (cols - 1) * spacing,
            rows * cellSize + (rows - 1) * spacing);

    private static Vector2Int Clamp(int row, int col, Vector2Int gridSize)
    {
        col = Mathf.Clamp(col, 0, gridSize.x - 1);
        row = Mathf.Clamp(row, 0, gridSize.y - 1);
        return new Vector2Int(col, row);
    }
}
