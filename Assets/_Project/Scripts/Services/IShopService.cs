namespace Services
{
    /// <summary>
    /// 商店服务:以积分(Points,由 IPointsService 统一管理)为唯一货币的商品购买与刷新。
    /// 实现:InventorySystem.Shop.ShopManager(DDOL 场景组件,挂在 ScopeContainer 下)。
    /// 交易路由(按源/目标网格类型分流)在 ItemView,经济操作在此收拢。
    /// </summary>
    public interface IShopService
    {
        /// <summary>商品价格(积分)。无定义的临时物品价格为 0。</summary>
        int GetPrice(ItemVM item);

        /// <summary>
        /// 从商店购买:扣除永久 Points。积分不足返回 false(不扣费,由拖放流程回滚到商店原位)。
        /// 注意:购买成功后商品的放置由调用方负责,本方法只处理费用。
        /// </summary>
        bool TryPurchase(ItemVM item);

        /// <summary>将商品放回商店并退还其价格。</summary>
        void Refund(ItemVM item);

        /// <summary>清空商店网格并重新填充商品,直至网格填满(目录用尽则停止)。</summary>
        void RefreshShop(GridView shopGrid);
    }
}
