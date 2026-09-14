using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class ItemShapeSetTests
{
    [Test]
    public void Plus3_UsesFiveUniqueCellsInThreeByThreeBounds()
    {
        IReadOnlyList<Vector2Int> points = GridUtilities.ShapeFactory.Plus3();

        Assert.That(points, Has.Count.EqualTo(5));
        Assert.That(new HashSet<Vector2Int>(points), Has.Count.EqualTo(5));
        Assert.That(GridUtilities.RotationHelper.GetBoundaryBox(points).width, Is.EqualTo(3));
        Assert.That(GridUtilities.RotationHelper.GetBoundaryBox(points).height, Is.EqualTo(3));
        Assert.That(
            points,
            Is.EquivalentTo(new[]
            {
                new Vector2Int(1, 0),
                new Vector2Int(1, 1),
                new Vector2Int(1, 2),
                new Vector2Int(0, 1),
                new Vector2Int(2, 1),
            }));
    }

    [Test]
    public void Plus5_UsesNineUniqueCellsInFiveByFiveBounds()
    {
        IReadOnlyList<Vector2Int> points = GridUtilities.ShapeFactory.Plus5();

        Assert.That(points, Has.Count.EqualTo(9));
        Assert.That(new HashSet<Vector2Int>(points), Has.Count.EqualTo(9));
        Assert.That(GridUtilities.RotationHelper.GetBoundaryBox(points).width, Is.EqualTo(5));
        Assert.That(GridUtilities.RotationHelper.GetBoundaryBox(points).height, Is.EqualTo(5));
        Assert.That(
            points,
            Is.EquivalentTo(new[]
            {
                new Vector2Int(2, 0),
                new Vector2Int(2, 1),
                new Vector2Int(2, 2),
                new Vector2Int(2, 3),
                new Vector2Int(2, 4),
                new Vector2Int(0, 2),
                new Vector2Int(1, 2),
                new Vector2Int(3, 2),
                new Vector2Int(4, 2),
            }));
    }

    [TestCase(ItemShape.Plus3, 5, 3)]
    [TestCase(ItemShape.Plus5, 9, 5)]
    public void DefaultShapeSet_ResolvesPlusShape(ItemShape shape, int expectedCellCount, int expectedSize)
    {
        IReadOnlyList<Vector2Int> points = ItemShapeSet.Default.GetPoints(shape);

        Assert.That(points, Has.Count.EqualTo(expectedCellCount));
        Assert.That(GridUtilities.RotationHelper.GetBoundaryBox(points).width, Is.EqualTo(expectedSize));
        Assert.That(GridUtilities.RotationHelper.GetBoundaryBox(points).height, Is.EqualTo(expectedSize));
    }

    [TestCase(ItemShape.Plus3, 3)]
    [TestCase(ItemShape.Plus5, 5)]
    public void PlusShape_RotatesWithoutChangingItsSquareBounds(ItemShape shape, int expectedSize)
    {
        ItemVM item = new(ItemShapeSet.Default.GetPoints(shape));
        item.SetDirection(Dir.Left);

        Assert.That(item.Width, Is.EqualTo(expectedSize));
        Assert.That(item.Height, Is.EqualTo(expectedSize));
        Assert.That(new HashSet<Vector2Int>(item.CoordinateSet), Has.Count.EqualTo(item.BasePoints.Count));

        GridVM grid = new(expectedSize, expectedSize, GridType.StorageForPlay);
        Assert.That(grid.Place(item, 0, 0), Is.True);
        Assert.That(grid.ItemCount, Is.EqualTo(1));
    }
}
