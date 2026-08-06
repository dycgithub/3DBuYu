using System;
using InventorySystem;

namespace GameSystem
{
    [Serializable]
    public class InventorySaveData
    {
        public OwnedItem[] items;
    }

    [Serializable]
    public class OwnedItem
    {
        public string itemConfigId;
        public string label;
        public int row;
        public int col;
        public int rotation;
        public int currentUses;
        public float currentDurability;
    }
}
