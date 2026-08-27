using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 拖拽影子(参考 Cholopol TIS 的 TetrisItemGhostView):
/// 按形状逐格渲染的半透明方块(与物品外观一致),跟随鼠标,在归属网格内吸附到格子。
/// </summary>
public class ItemGhostView : MonoBehaviour
{
    public RectTransform Rect { get; set; }
    public Image Image { get; set; }
    private readonly List<Image> cells = new();

    private void Awake()
    {
        Rect = GetComponent<RectTransform>();
        Image = GetComponent<Image>();
    }

    /// <summary>初始化:尺寸 = 形状包围盒,方块 = 形状(半透明)。</summary>
    public void Init(GridView grid, ItemVM item)
    {
        Rect.anchorMin = new Vector2(0f, 1f);
        Rect.anchorMax = new Vector2(0f, 1f);
        Rect.pivot = new Vector2(0f, 1f);
        Rect.sizeDelta = grid.SizeFor(item);
        Image.raycastTarget = false;
        Image.color = Color.clear; // 外壳不显示,形状由方块表达
        RebuildCells(grid, item);
    }

    /// <summary>按形状重建半透明方块(旋转后调用)。</summary>
    public void RebuildCells(GridView grid, ItemVM item)
    {
        for (int i = 0; i < cells.Count; i++)
            if (cells[i] != null)
                Destroy(cells[i].gameObject);
        cells.Clear();

        // 影子外观跟随物品(定义图标/颜色优先,回退网格底图),半透明表达"预览"
        Color baseColor = ItemView.ResolveColor(item, grid.CellColor);
        Color ghostColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.5f);
        cells.AddRange(ItemView.BuildCells(Rect, item.CoordinateSet, item.RotationOffset,
            grid.CellSize, grid.Step, ItemView.ResolveSprite(item, grid.CellSprite), ghostColor));
    }

    public void SetAnchoredPos(Vector2 pos) => Rect.anchoredPosition = pos;
    public void Follow(Vector2 screenPos) => Rect.position = screenPos;
}