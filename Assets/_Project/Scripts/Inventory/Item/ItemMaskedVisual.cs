using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 用真实占用格生成 UGUI Mask，并裁切一张覆盖物品包围盒的图片。
/// 遮罩只负责形状几何，物品输入仍由 ItemView 的透明外壳接收。
/// </summary>
[RequireComponent(typeof(Mask))]
[RequireComponent(typeof(CanvasRenderer))]
public sealed class ItemMaskedVisual : MaskableGraphic
{
    private readonly List<Rect> _cellRects = new();

    private Image _artwork;
    private Mask _mask;

    /// <summary>当前遮罩使用的占用格数量。</summary>
    public int OccupiedCellCount => _cellRects.Count;

    protected override void Awake()
    {
        base.Awake();
        _mask = GetComponent<Mask>();
        _mask.showMaskGraphic = false;
        raycastTarget = false;
        ResolveArtwork();
    }

    /// <summary>
    /// 根据当前方向的形状和包围盒图片刷新遮罩。
    /// </summary>
    /// <param name="points">当前方向下的基础形状点集。</param>
    /// <param name="rotationOffset">当前方向的包围盒对齐偏移。</param>
    /// <param name="cellSize">网格单元可见尺寸。</param>
    /// <param name="step">网格单元之间的步长。</param>
    /// <param name="sprite">覆盖包围盒的图片。</param>
    /// <param name="tint">图片颜色叠加。</param>
    /// <param name="currentSize">当前方向的包围盒尺寸。</param>
    /// <param name="artworkSize">图片原始方向的包围盒尺寸。</param>
    /// <param name="imageAngle">图片跟随物品方向的旋转角度。</param>
    public void Configure(
        IReadOnlyList<Vector2Int> points,
        Vector2Int rotationOffset,
        float cellSize,
        float step,
        Sprite sprite,
        Color tint,
        Vector2 currentSize,
        Vector2 artworkSize,
        float imageAngle)
    {
        RectTransform rect = rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = currentSize;

        _cellRects.Clear();
        _cellRects.AddRange(CalculateCellRects(points, rotationOffset, cellSize, step));

        ResolveArtwork();
        if (_artwork != null)
        {
            RectTransform artworkRect = _artwork.rectTransform;
            artworkRect.anchorMin = new Vector2(0.5f, 0.5f);
            artworkRect.anchorMax = new Vector2(0.5f, 0.5f);
            artworkRect.pivot = new Vector2(0.5f, 0.5f);
            artworkRect.anchoredPosition = Vector2.zero;
            artworkRect.sizeDelta = artworkSize;
            artworkRect.localRotation = Quaternion.Euler(0f, 0f, -imageAngle);

            _artwork.sprite = sprite;
            _artwork.color = tint;
            _artwork.preserveAspect = false;
            _artwork.raycastTarget = false;
            _artwork.gameObject.SetActive(sprite != null);
        }

        _mask.showMaskGraphic = false;
        raycastTarget = false;
        SetVerticesDirty();
        SetMaterialDirty();
    }

    /// <summary>
    /// 计算占用格在包围盒内的局部矩形，矩形坐标使用左上角原点。
    /// </summary>
    public static IReadOnlyList<Rect> CalculateCellRects(
        IReadOnlyList<Vector2Int> points,
        Vector2Int rotationOffset,
        float cellSize,
        float step)
    {
        var result = new List<Rect>();
        if (points == null || cellSize <= 0f || step <= 0f)
        {
            return result;
        }

        var occupied = new HashSet<Vector2Int>();
        for (int i = 0; i < points.Count; i++)
        {
            Vector2Int point = points[i] + rotationOffset;
            if (!occupied.Add(point))
            {
                continue;
            }

            result.Add(new Rect(
                point.x * step,
                -point.y * step - cellSize,
                cellSize,
                cellSize));
        }

        return result;
    }

    protected override void OnPopulateMesh(VertexHelper vertexHelper)
    {
        vertexHelper.Clear();
        for (int i = 0; i < _cellRects.Count; i++)
        {
            Rect cell = _cellRects[i];
            int start = vertexHelper.currentVertCount;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = Color.white;

            vertex.position = new Vector3(cell.xMin, cell.yMin, 0f);
            vertexHelper.AddVert(vertex);
            vertex.position = new Vector3(cell.xMin, cell.yMax, 0f);
            vertexHelper.AddVert(vertex);
            vertex.position = new Vector3(cell.xMax, cell.yMax, 0f);
            vertexHelper.AddVert(vertex);
            vertex.position = new Vector3(cell.xMax, cell.yMin, 0f);
            vertexHelper.AddVert(vertex);

            vertexHelper.AddTriangle(start, start + 1, start + 2);
            vertexHelper.AddTriangle(start + 2, start + 3, start);
        }
    }

    private void ResolveArtwork()
    {
        if (_artwork == null)
        {
            _artwork = GetComponentInChildren<Image>(true);
        }
    }
}
