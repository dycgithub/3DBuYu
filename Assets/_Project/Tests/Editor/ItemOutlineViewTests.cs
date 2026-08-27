using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class ItemOutlineViewTests
{
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
}
