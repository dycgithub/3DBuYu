using Services;
using UnityEngine;
using VContainer;

namespace InventorySystem.Shop
{
    /// <summary>
    /// 商店服务(DDOL 场景组件,挂在 ScopeContainer 下,注册为 IShopService):
    /// - RefreshShop:进入 UIScene 时清空商店网格,按面积权重重新填充商品。
    /// - TryPurchase:从商店购买(扣除永久 Points)。积分不足返回 false,由拖放流程回滚。
    /// - Refund:将商品放回商店并退还购买价格。
    /// 积分统一走 IPointsService(ResourceManager),是本项目唯一货币。
    /// </summary>
    public class ShopManager : MonoBehaviour, IShopService
    {
        [Inject] private IPointsService _points;
        [Inject] private ItemCatalog _catalog;
        [Inject] private IUINotificationService _notify;

        [Header("商店配置")]
        [SerializeField, Min(1)] private int fillPassLimit = 32; // 生成尝试基数,防止"放不下"死循环

        public int GetPrice(ItemVM item) => item?.Definition != null ? item.Definition.Price : 0;

        /// <summary>
        /// 从商店购买:扣除永久 Points。积分不足返回 false(不扣费,由拖放流程把商品放回商店原位)。
        /// </summary>
        public bool TryPurchase(ItemVM item)
        {
            int price = GetPrice(item);
            if (price <= 0) return true; // 免费商品直接放行

            if (!_points.HasEnoughPoints(price))
            {
                _notify?.ShowNotification($"积分不足,无法购买(需要 {price})", NotificationKind.Warning);
                return false;
            }

            _points.SpendPoints(price, "商店购买");
            _notify?.ShowNotification($"购买成功,花费 {price} 积分", NotificationKind.Success);
            return true;
        }

        /// <summary>将商品放回商店并退还其价格。</summary>
        public void Refund(ItemVM item)
        {
            int price = GetPrice(item);
            if (price <= 0)
                return;

            _points.AddPoints(price, "商店退款");
            _notify?.ShowNotification($"已退款 {price} 积分", NotificationKind.Success);
        }

        /// <summary>进入 UIScene 时调用:清空商店网格,随即填充商品直至填满。</summary>
        public void RefreshShop(GridView shopGrid)
        {
            if (shopGrid == null)
            {
                Debug.LogWarning("[ShopManager] RefreshShop: shopGrid 为空", this);
                return;
            }

            shopGrid.EnsureGridVM();
            shopGrid.ClearAll();
            FillShop(shopGrid);
        }

        /// <summary>按 ItemCatalog 中的面积权重生成不重叠的包围盒。</summary>
        private void FillShop(GridView grid)
        {
            if (_catalog == null || _catalog.Items == null || _catalog.Items.Count == 0)
            {
                Debug.LogWarning("[ShopManager] ItemCatalog 为空,无法填充商店", this);
                return;
            }

            int attemptLimit = Mathf.Max(1, fillPassLimit) * Mathf.Max(1, _catalog.Items.Count);
            var generator = new ShopPlacementGenerator();
            var placements = generator.Generate(
                _catalog.Items,
                grid.ShapeSet,
                grid.GridVM.Width,
                grid.GridVM.Height,
                _catalog.GetShopAreaWeight,
                attemptLimit);

            for (int i = 0; i < placements.Count; i++)
            {
                var placement = placements[i];
                var item = new ItemVM(placement.Definition, grid.ShapeSet);
                item.SetDirection(placement.Direction);
                if (grid.SpawnItem(item, placement.Column, placement.Row) == null)
                {
                    Debug.LogWarning(
                        $"[ShopManager] 商店生成失败: {placement.Definition?.name} ({placement.Column},{placement.Row})",
                        this);
                }
            }
        }
    }
}
