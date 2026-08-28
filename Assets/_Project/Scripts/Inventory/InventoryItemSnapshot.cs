using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 网格物品的跨场景快照。
/// 快照不持有 ItemVM 或 ItemView，只复制恢复所需的静态定义和网格状态。
/// </summary>
public sealed class InventoryItemSnapshot
{
    private readonly IReadOnlyList<Vector2Int> _basePoints;

    public ItemDefinition Definition { get; }
    public int InstanceId { get; }
    public IReadOnlyList<Vector2Int> BasePoints => _basePoints;
    public Dir Direction { get; }
    public Vector2Int LocalGridCoordinate { get; }

    public InventoryItemSnapshot(ItemVM source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));

        Definition = source.Definition;
        InstanceId = source.InstanceId;
        _basePoints = new List<Vector2Int>(source.BasePoints ?? Array.Empty<Vector2Int>()).AsReadOnly();
        Direction = source.Direction;
        LocalGridCoordinate = source.LocalGridCoordinate;
    }

    /// <summary>由快照创建新的 ItemVM，确保目标网格不复用源实例。</summary>
    public ItemVM CreateItem()
    {
        var item = new ItemVM(
            Definition,
            origin: LocalGridCoordinate,
            basePoints: _basePoints,
            instanceId: InstanceId);
        item.SetDirection(Direction);
        return item;
    }
}
