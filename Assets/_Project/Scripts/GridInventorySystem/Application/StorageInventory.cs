using InventorySystem;

namespace TurretSystem
{
    /// <summary>
    /// 玩家通用库存网格。继承 BaseInventory，接受所有物品类型。
    /// 作为商店购买后的落点，玩家再将物品从这里拖到装备网格。
    /// 库存中的物品不影响任何属性（使用 NullAttributesAggregator）。
    /// </summary>
    public class StorageInventory : BaseInventory
    {
        /// <summary>库存所有者（Turret 引用，用于 PortExpander 等）。</summary>
        public Turret Owner { get; }

        public StorageInventory(Turret owner, int columns = 8, int rows = 8)
            : base(columns, rows, new AnyItemValidator(), new NullAttributesAggregator())
        {
            Owner = owner;
        }
    }
}
