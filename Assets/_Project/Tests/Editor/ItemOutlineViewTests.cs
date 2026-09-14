using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public sealed class ItemOutlineViewTests
{
    private static readonly Color NormalColor = new(1f, 1f, 1f, 0.35f);
    private static readonly Color HoverColor = new(0.2f, 1f, 0.2f, 1f);
    private static readonly Color SelectedColor = new(1f, 0.85f, 0.15f, 1f);

    private GameObject _testObject;

    [TearDown]
    public void TearDown()
    {
        if (_testObject != null)
            Object.DestroyImmediate(_testObject);
    }

    [Test]
    public void SquareShape_OnlyHasFourExternalSegments()
    {
        var points = new[]
        {
            new Vector2Int(0, 0),
            new Vector2Int(1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(1, 1),
        };

        var segments = ItemOutlineView.CalculateSegments(points, Vector2Int.zero);

        Assert.That(segments, Has.Count.EqualTo(4));
        Assert.That(segments.Count(segment => segment.Horizontal), Is.EqualTo(2));
        Assert.That(segments.Count(segment => !segment.Horizontal), Is.EqualTo(2));
    }

    [Test]
    public void TShape_DoesNotDrawBoundingBoxBottomAsOneContinuousEdge()
    {
        var points = new[]
        {
            new Vector2Int(0, 0),
            new Vector2Int(1, 0),
            new Vector2Int(2, 0),
            new Vector2Int(1, 1),
        };

        var segments = ItemOutlineView.CalculateSegments(points, Vector2Int.zero);

        Assert.That(segments, Has.Count.EqualTo(8));
        Assert.That(
            segments.Any(segment => segment.Horizontal && segment.Line == 1 &&
                                    segment.Start == 0 && segment.End == 3),
            Is.False);
    }

    [Test]
    public void OffsetShape_TranslatesEveryOccupiedEdge()
    {
        var points = new[]
        {
            new Vector2Int(0, 0),
            new Vector2Int(0, 1),
        };

        var segments = ItemOutlineView.CalculateSegments(points, new Vector2Int(3, 4));

        Assert.That(segments.All(segment => segment.Line >= 3), Is.True);
        Assert.That(segments.Any(segment => !segment.Horizontal && segment.Line == 3), Is.True);
        Assert.That(segments.Any(segment => segment.Horizontal && segment.Line == 4), Is.True);
    }

    [Test]
    public void HoveredOutline_UsesGreenStyle()
    {
        ItemOutlineView outline = CreateOutline();
        outline.Rebuild(new[] { Vector2Int.zero }, Vector2Int.zero, 40f, 50f);

        outline.SetHovered(true);

        Assert.That(GetSegmentImages(outline).All(image => image.color == HoverColor), Is.True);
    }

    [Test]
    public void SelectedOutline_TakesPriorityOverHoverStyle()
    {
        ItemOutlineView outline = CreateOutline();
        outline.Rebuild(new[] { Vector2Int.zero }, Vector2Int.zero, 40f, 50f);

        outline.SetHovered(true);
        outline.SetSelected(true);

        Assert.That(GetSegmentImages(outline).All(image => image.color == SelectedColor), Is.True);

        outline.SetHovered(false);

        Assert.That(GetSegmentImages(outline).All(image => image.color == SelectedColor), Is.True);
    }

    [Test]
    public void ClearingHover_RestoresNormalOutlineStyle()
    {
        ItemOutlineView outline = CreateOutline();
        outline.Rebuild(new[] { Vector2Int.zero }, Vector2Int.zero, 40f, 50f);

        outline.SetHovered(true);
        outline.SetHovered(false);

        Assert.That(GetSegmentImages(outline).All(image => image.color == NormalColor), Is.True);
    }

    private ItemOutlineView CreateOutline()
    {
        _testObject = new GameObject("ItemOutlineTest", typeof(RectTransform));
        return _testObject.AddComponent<ItemOutlineView>();
    }

    private static Image[] GetSegmentImages(ItemOutlineView outline)
    {
        return outline.GetComponentsInChildren<Image>(true);
    }
}
