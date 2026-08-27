using NUnit.Framework;
using UnityEngine;

public sealed class InventoryTransferStorageTests
{
    private ItemDefinition _definition;

    [SetUp]
    public void SetUp()
    {
        _definition = ScriptableObject.CreateInstance<ItemDefinition>();
    }

    [TearDown]
    public void TearDown()
    {
        if (_definition != null)
            Object.DestroyImmediate(_definition);
    }

    [Test]
    public void Snapshot_PreservesDefinitionShapeDirectionAndCoordinate()
    {
        var source = new ItemVM(
            _definition,
            origin: new Vector2Int(3, 2),
            basePoints: new[] { Vector2Int.zero, Vector2Int.right });
        source.SetDirection(Dir.Left);

        var snapshot = new InventoryItemSnapshot(source);
        var restored = snapshot.CreateItem();

        Assert.That(restored, Is.Not.SameAs(source));
        Assert.That(restored.Definition, Is.SameAs(_definition));
        Assert.That(restored.Direction, Is.EqualTo(Dir.Left));
        Assert.That(restored.LocalGridCoordinate, Is.EqualTo(new Vector2Int(3, 2)));
        Assert.That(restored.BasePoints, Is.EqualTo(new[] { Vector2Int.zero, Vector2Int.right }));
    }

    [Test]
    public void Snapshot_DeepCopiesBasePoints()
    {
        var source = new ItemVM(
            _definition,
            origin: Vector2Int.zero,
            basePoints: new[] { Vector2Int.zero });
        var snapshot = new InventoryItemSnapshot(source);

        source.BasePoints = new[] { Vector2Int.one };

        Assert.That(snapshot.BasePoints, Is.EqualTo(new[] { Vector2Int.zero }));
    }

    [Test]
    public void ReplaceAndClear_ManagePendingSnapshots()
    {
        var source = new ItemVM(_definition);
        var storage = new InventoryTransferStorage();
        var snapshot = new InventoryItemSnapshot(source);

        storage.Replace(new[] { snapshot });

        Assert.That(storage.HasPendingItems, Is.True);
        Assert.That(storage.PendingItems.Count, Is.EqualTo(1));
        Assert.That(storage.PendingItems[0], Is.SameAs(snapshot));

        storage.Clear();

        Assert.That(storage.HasPendingItems, Is.False);
        Assert.That(storage.PendingItems, Is.Empty);
    }
}
