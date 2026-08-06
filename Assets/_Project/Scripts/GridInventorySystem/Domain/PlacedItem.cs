using System;

namespace InventorySystem
{
    /// <summary>
    /// 运行时放置记录（可序列化用于存档）。
    /// 存储系统：网格中某个物品实例的位置/旋转快照，由 InventoryGrid 维护。
    /// </summary>
    [Serializable]
    public struct PlacedItem
    {
        public int instanceId;
        public string itemConfigId;
        public int row;
        public int col;
        public int rotation;
    }
}
