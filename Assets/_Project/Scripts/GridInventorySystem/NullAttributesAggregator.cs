using System.Collections.Generic;

namespace TurretSystem
{
    /// <summary>
    /// 空属性聚合器 — 用于不参与属性聚合的库存（如 StorageInventory）。
    /// 所有方法均为 no-op，OnAttributesChanged 不会被触发。
    /// </summary>
    public class NullAttributesAggregator : Interfaces.IAttributesAggregator
    {
        public event System.Action OnAttributesChanged
        {
            add { }
            remove { }
        }

        public void Recalculate(IReadOnlyList<InventorySystem.PlacedItem> items, InventorySystem.InventoryGrid grid) { }

        public void ResetToBase() { }

        public string GetDescription() => "库存（无属性加成）";
    }
}
