using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 绘制物品真实占用形状的外部轮廓。
/// 轮廓基于占用格边缘生成，不使用 ItemView 包围盒，因此可以正确表现 T 形、L 形等非矩形物品。
/// </summary>
public sealed class ItemOutlineView : MonoBehaviour
{
    /// <summary>一条逻辑轮廓线段，坐标使用物品局部格坐标。</summary>
    public readonly struct OutlineSegment
    {
        public bool Horizontal { get; }
        public int Line { get; }
        public int Start { get; }
        public int End { get; }
        public float Position { get; }

        public OutlineSegment(bool horizontal, int line, int start, int end, float position)
        {
            Horizontal = horizontal;
            Line = line;
            Start = start;
            End = end;
            Position = position;
        }
    }

    [Header("轮廓")]
    [SerializeField] private bool visible = true;
    [SerializeField] private Color normalColor = new(1f, 1f, 1f, 0.35f);
    [SerializeField] private Color selectedColor = new(1f, 0.85f, 0.15f, 1f);
    [SerializeField, Min(0.1f)] private float normalThickness = 2f;
    [SerializeField, Min(0.1f)] private float selectedThickness = 4f;

    private readonly List<Image> _segments = new();
    private RectTransform _rect;
    private bool _selected;

    /// <summary>当前是否显示轮廓。</summary>
    public bool IsVisible => visible;

    /// <summary>当前是否使用选中样式。</summary>
    public bool IsSelected => _selected;

    /// <summary>当前生成的轮廓线段数量。</summary>
    public int SegmentCount => _segments.Count;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        ConfigureRectTransform();
    }

    /// <summary>
    /// 根据当前方向下的占用点重建轮廓。
    /// </summary>
    /// <param name="points">物品基础形状点集。</param>
    /// <param name="rotationOffset">旋转后用于对齐左上角的偏移。</param>
    /// <param name="cellSize">单格可见尺寸。</param>
    /// <param name="step">相邻格中心之间的步长。</param>
    public void Rebuild(
        IReadOnlyList<Vector2Int> points,
        Vector2Int rotationOffset,
        float cellSize,
        float step)
    {
        ClearSegments();
        ConfigureRectTransform();

        if (points == null || points.Count == 0 || cellSize <= 0f || step <= 0f)
            return;

        var occupied = new HashSet<Vector2Int>();
        for (int i = 0; i < points.Count; i++)
            occupied.Add(points[i] + rotationOffset);

        var generated = new List<OutlineSegment>(points.Count * 2);
        foreach (var cell in occupied)
        {
            if (!occupied.Contains(new Vector2Int(cell.x, cell.y - 1)))
                AddHorizontal(generated, cell.x, cell.y, cellSize, step);
            if (!occupied.Contains(new Vector2Int(cell.x, cell.y + 1)))
                AddHorizontal(generated, cell.x, cell.y + 1, cellSize, step, -cell.y * step - cellSize);
            if (!occupied.Contains(new Vector2Int(cell.x - 1, cell.y)))
                AddVertical(generated, cell.x, cell.y, cellSize, step);
            if (!occupied.Contains(new Vector2Int(cell.x + 1, cell.y)))
                AddVertical(generated, cell.x + 1, cell.y, cellSize, step, cell.x * step + cellSize);
        }

        MergeSegments(generated);
        for (int i = 0; i < generated.Count; i++)
            _segments.Add(CreateSegment(generated[i], cellSize, step));

        ApplyStyle();
    }

    /// <summary>切换轮廓是否可见。</summary>
    public void SetVisible(bool value)
    {
        visible = value;
        ApplyStyle();
    }

    /// <summary>切换普通轮廓和选中轮廓样式。</summary>
    public void SetSelected(bool value)
    {
        _selected = value;
        ApplyStyle();
    }

    /// <summary>
    /// 计算指定形状的外部轮廓线段，供运行时代码和测试复用。
    /// </summary>
    /// <param name="points">基础形状点集。</param>
    /// <param name="rotationOffset">旋转对齐偏移。</param>
    /// <returns>已去除内部边并合并连续边的轮廓线段。</returns>
    public static IReadOnlyList<OutlineSegment> CalculateSegments(
        IReadOnlyList<Vector2Int> points,
        Vector2Int rotationOffset)
    {
        if (points == null || points.Count == 0)
            return new List<OutlineSegment>();

        var occupied = new HashSet<Vector2Int>();
        for (int i = 0; i < points.Count; i++)
            occupied.Add(points[i] + rotationOffset);

        var result = new List<OutlineSegment>(points.Count * 2);
        foreach (var cell in occupied)
        {
            if (!occupied.Contains(new Vector2Int(cell.x, cell.y - 1)))
                result.Add(new OutlineSegment(true, cell.y, cell.x, cell.x + 1, 0f));
            if (!occupied.Contains(new Vector2Int(cell.x, cell.y + 1)))
                result.Add(new OutlineSegment(true, cell.y + 1, cell.x, cell.x + 1, 1f));
            if (!occupied.Contains(new Vector2Int(cell.x - 1, cell.y)))
                result.Add(new OutlineSegment(false, cell.x, cell.y, cell.y + 1, 0f));
            if (!occupied.Contains(new Vector2Int(cell.x + 1, cell.y)))
                result.Add(new OutlineSegment(false, cell.x + 1, cell.y, cell.y + 1, 1f));
        }

        MergeSegments(result);
        return result;
    }

    private void ConfigureRectTransform()
    {
        if (_rect == null)
            return;

        _rect.anchorMin = new Vector2(0f, 1f);
        _rect.anchorMax = new Vector2(0f, 1f);
        _rect.pivot = new Vector2(0f, 1f);
        _rect.anchoredPosition = Vector2.zero;
        if (transform.parent is RectTransform parent)
            _rect.sizeDelta = parent.rect.size;
    }

    private static void AddHorizontal(
        List<OutlineSegment> destination,
        int start,
        int line,
        float cellSize,
        float step,
        float position = float.NaN)
    {
        destination.Add(new OutlineSegment(
            true,
            line,
            start,
            start + 1,
            float.IsNaN(position) ? -line * step : position));
    }

    private static void AddVertical(
        List<OutlineSegment> destination,
        int line,
        int start,
        float cellSize,
        float step,
        float position = float.NaN)
    {
        destination.Add(new OutlineSegment(
            false,
            line,
            start,
            start + 1,
            float.IsNaN(position) ? line * step : position));
    }

    private static void MergeSegments(List<OutlineSegment> segments)
    {
        for (int i = 0; i < segments.Count; i++)
        {
            for (int j = i + 1; j < segments.Count; j++)
            {
                OutlineSegment a = segments[i];
                OutlineSegment b = segments[j];
                if (a.Horizontal != b.Horizontal || a.Line != b.Line ||
                    !Mathf.Approximately(a.Position, b.Position))
                    continue;

                if (a.End == b.Start)
                {
                    segments[i] = new OutlineSegment(a.Horizontal, a.Line, a.Start, b.End, a.Position);
                    segments.RemoveAt(j--);
                }
                else if (b.End == a.Start)
                {
                    segments[i] = new OutlineSegment(a.Horizontal, a.Line, b.Start, a.End, a.Position);
                    segments.RemoveAt(j--);
                }
            }
        }
    }

    private Image CreateSegment(OutlineSegment segment, float cellSize, float step)
    {
        var go = new GameObject("OutlineSegment", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        if (segment.Horizontal)
        {
            float left = segment.Start * step;
            float right = (segment.End - 1) * step + cellSize;
            rect.sizeDelta = new Vector2(Mathf.Max(0.1f, right - left), normalThickness);
            rect.anchoredPosition = new Vector2((left + right) * 0.5f, segment.Position);
        }
        else
        {
            float top = -segment.Start * step;
            float bottom = -(segment.End - 1) * step - cellSize;
            rect.sizeDelta = new Vector2(normalThickness, Mathf.Max(0.1f, top - bottom));
            rect.anchoredPosition = new Vector2(segment.Position, (top + bottom) * 0.5f);
        }

        var image = go.GetComponent<Image>();
        image.sprite = GridUtilities.WhiteSprite;
        image.raycastTarget = false;
        return image;
    }

    private void ApplyStyle()
    {
        Color color = _selected ? selectedColor : normalColor;
        float thickness = _selected ? selectedThickness : normalThickness;
        for (int i = 0; i < _segments.Count; i++)
        {
            Image image = _segments[i];
            if (image == null)
                continue;

            image.enabled = visible;
            image.color = color;
            var rect = image.rectTransform;
            if (rect.sizeDelta.x <= rect.sizeDelta.y)
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, thickness);
            else
                rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, thickness);
        }
    }

    private void ClearSegments()
    {
        for (int i = 0; i < _segments.Count; i++)
        {
            if (_segments[i] != null)
                Destroy(_segments[i].gameObject);
        }

        _segments.Clear();
    }
}
