using InventorySystem;
using TurretSystem;

namespace InventorySystem.Shop
{
    /// <summary>
    /// 商店货架网格。继承 BaseInventory，与仓库一致的数据模型。
    /// 商品按形状占格，购买时物品转移到目标网格并从货架移除（拖拽即转移）。
    /// </summary>
    public class ShopInventory : BaseInventory
    {
        /// <summary>商店管理器（购买/刷新逻辑的持有者）。</summary>
        public ShopManager Owner { get; }

        public ShopInventory(ShopManager owner, int columns = 9, int rows = 9)
            : base(columns, rows, new AnyItemValidator(), new NullAttributesAggregator())
        {
            Owner = owner;
        }
    }
}
