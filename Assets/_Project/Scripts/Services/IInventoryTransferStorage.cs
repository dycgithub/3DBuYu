using System.Collections.Generic;

namespace Services
{
    /// <summary>
    /// 跨 UI 场景与 Game 场景传递网格物品的临时存储。
    /// 该服务只保存内存快照，不保存场景对象或 ItemVM 实例。
    /// </summary>
    public interface IInventoryTransferStorage
    {
        /// <summary>当前是否存在等待 Game 场景恢复的物品。</summary>
        bool HasPendingItems { get; }

        /// <summary>等待恢复的物品快照，只读访问。</summary>
        IReadOnlyList<InventoryItemSnapshot> PendingItems { get; }

        /// <summary>用新的快照集合替换当前内容。</summary>
        void Replace(IEnumerable<InventoryItemSnapshot> snapshots);

        /// <summary>清空临时快照。</summary>
        void Clear();
    }
}
