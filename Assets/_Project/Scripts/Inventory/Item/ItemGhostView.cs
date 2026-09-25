using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 拖拽影子：优先用包围盒图片配合遮罩显示物品形状，没有包围盒图片时回退为逐格渲染。
/// 跟随鼠标，并在归属网格内吸附到格子。
/// </summary>
public class ItemGhostView : MonoBehaviour
{
    public RectTransform Rect { get; set; }
    public Image Image { get; set; }
    private readonly List<Image> cells = new();
    private ItemMaskedVisual maskedVisual;

    private void Awake()
    {
        Rect = GetComponent<RectTransform>();
        Image = GetComponent<Image>();
        maskedVisual = GetComponentInChildren<ItemMaskedVisual>(true);
    }

    /// <summary>初始化拖拽影子的包围盒、透明外壳和形状显示。</summary>
    public void Init(GridView grid, ItemVM item)
    {
        Rect.anchorMin = new Vector2(0f, 1f);
        Rect.anchorMax = new Vector2(0f, 1f);
        Rect.pivot = new Vector2(0f, 1f);
        Rect.sizeDelta = grid.SizeFor(item);
        Image.raycastTarget = false;
        Image.color = Color.clear; // 外壳不显示，形状由后续分支的图片遮罩或逐格图形表达
        RebuildCells(grid, item);
    }

    /// <summary>按当前方向重建图片遮罩或逐格形状显示。</summary>
    public void RebuildCells(GridView grid, ItemVM item)
    {
        for (int i = 0; i < cells.Count; i++)
            if (cells[i] != null)
                Destroy(cells[i].gameObject);
        cells.Clear();

        Color baseColor = ItemView.ResolveColor(item, grid.CellColor);
        Color ghostColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.5f);
        Sprite maskedSprite = item?.Definition?.BoundingBoxImage;
        if (maskedSprite != null)
        {
            EnsureMaskedVisual();
            maskedVisual.gameObject.SetActive(true);
            float spacing = grid.Step - grid.CellSize;
            (int width, int height) = GridUtilities.RotationHelper.GetBoundaryBox(item.BasePoints);
            Vector2 artworkSize = new(
                width * grid.CellSize + Mathf.Max(0, width - 1) * spacing,
                height * grid.CellSize + Mathf.Max(0, height - 1) * spacing);
            maskedVisual.Configure(
                item.CoordinateSet,
                item.RotationOffset,
                grid.CellSize,
                grid.Step,
                maskedSprite,
                ghostColor,
                grid.SizeFor(item),
                artworkSize,
                GridUtilities.RotationHelper.GetRotationAngle(item.Direction));
            return;
        }

        if (maskedVisual != null)
        {
            maskedVisual.gameObject.SetActive(false);
        }

        cells.AddRange(ItemView.BuildCells(Rect, item.CoordinateSet, item.RotationOffset,
            grid.CellSize, grid.Step, ItemView.ResolveSprite(item, grid.CellSprite), ghostColor));
    }

    private void EnsureMaskedVisual()
    {
        if (maskedVisual != null)
        {
            return;
        }

        GameObject maskObject = new("MaskedVisual", typeof(RectTransform), typeof(ItemMaskedVisual), typeof(Mask));
        maskObject.transform.SetParent(transform, false);
        GameObject imageObject = new("BoundingBoxImage", typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(maskObject.transform, false);
        maskedVisual = maskObject.GetComponent<ItemMaskedVisual>();
    }

    public void SetAnchoredPos(Vector2 pos) => Rect.anchoredPosition = pos;
    public void Follow(Vector2 screenPos) => Rect.position = screenPos;
}
