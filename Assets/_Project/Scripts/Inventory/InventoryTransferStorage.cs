using System.Collections.Generic;
using Services;

/// <summary>
/// UI 场景到 Game 场景之间的内存物品缓冲区。
/// 该对象由 ProjectLifetimeScope 以单例注册，场景卸载不会清除其中的快照。
/// </summary>
public sealed class InventoryTransferStorage : IInventoryTransferStorage
{
    private readonly List<InventoryItemSnapshot> _pendingItems = new();

    public bool HasPendingItems => _pendingItems.Count > 0;
    public IReadOnlyList<InventoryItemSnapshot> PendingItems => _pendingItems;

    public void Replace(IEnumerable<InventoryItemSnapshot> snapshots)
    {
        var replacement = new List<InventoryItemSnapshot>();
        if (snapshots != null)
        {
            foreach (var snapshot in snapshots)
            {
                if (snapshot != null)
                    replacement.Add(snapshot);
            }
        }

        _pendingItems.Clear();
        _pendingItems.AddRange(replacement);
    }

    public void Clear() => _pendingItems.Clear();
}
