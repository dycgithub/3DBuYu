using System;

namespace InventorySystem
{
    /// <summary>
    /// 背包网格中的单个格子(纯数据)。
    /// 可序列化用于存档。
    /// 原 GridSlot,为视图层 GridSlot(格子视图)让名,数据层更名 GridCell。
    /// </summary>
    [Serializable]
    public struct GridCell
    {
        public int row;
        public int col;
        public int itemInstanceId;
        public bool IsEmpty => itemInstanceId < 0;
    }
}
