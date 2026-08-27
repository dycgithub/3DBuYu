using UnityEngine;

[CreateAssetMenu(fileName = "PlacementConfig", menuName = "Grid/Placement Config")]
public class InventoryPlacementConfig : ScriptableObject
{
    [Header("高亮颜色")]
    [SerializeField] private Color validColor = new Color(0f, 1f, 0f, 100f / 255f);   // 可放置:绿
    [SerializeField] private Color invalidColor = new Color(1f, 0f, 0f, 100f / 255f); // 不可放置:红

    public Color ValidColor => validColor;
    public Color InvalidColor => invalidColor;
    
    /// <summary>全局默认配置(未指定资产时使用)。</summary>
    public static InventoryPlacementConfig Default { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitDefault()
    {
        Default = CreateInstance<InventoryPlacementConfig>();
    }

    /// <summary>
    /// 核心判定:形状覆盖的每格必须 ①在边界内 ②未被占用。
    /// col/row = 锚点(形状左上角)格子坐标。
    /// </summary>
    public bool Evaluate(GridVM grid, ItemVM item, int col, int row, out PlacementBlockReason reason)
    {
        if (grid == null || item == null)
        {
            reason = PlacementBlockReason.None;
            return false;
        }

        if (!grid.BoundaryCheck(col, row, item.Width, item.Height))
        {
            reason = PlacementBlockReason.OutOfBounds;
            return false;
        }

        foreach (var p in item.CoordinateSet)
        {
            int x = col + p.x + item.RotationOffset.x;
            int y = row + p.y + item.RotationOffset.y;
            if (grid[x,y] != null)
            {
                reason = PlacementBlockReason.Occupied;
                return false;
            }
        }
        reason = PlacementBlockReason.None;
        return true;
    }
}