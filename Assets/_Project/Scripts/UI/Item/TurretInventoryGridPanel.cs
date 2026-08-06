using Interfaces;
using InventorySystem;
using TurretSystem;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.UI.Item
{
    /// <summary>
    /// 已废弃:网格视图已统一为 <see cref="_Project.UI.Inventory.InventoryGridView"/>。
    /// 保留此薄包装仅为兼容场景中已序列化的组件引用,请迁移到 InventoryGridView。
    /// </summary>
    [System.Obsolete("Use _Project.UI.Inventory.InventoryGridView instead.")]
    public class TurretInventoryGridPanel : _Project.UI.Inventory.InventoryGridView
    {
        public void Initialize(TurretInventory inventory) => Initialize((IInventory)inventory);
    }
}
