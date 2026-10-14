using Services;
using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace InventorySystem.Shop
{
    /// <summary>
    /// 商店服务(DDOL 场景组件,挂在 ScopeContainer 下,注册为 IShopService):
    /// - RefreshShop:进入 UIScene 时免费生成商店；手动刷新先完整准备，再扣积分替换。
    /// - TryPurchase:从商店购买(扣除永久 Points)。积分不足返回 false,由拖放流程回滚。
    /// - Refund:将商品放回商店并退还购买价格。
    /// 积分统一走 IPointsService，购买和退款提示从当前语言翻译表读取。
    /// </summary>
    public class ShopManager : MonoBehaviour, IShopService
    {
        public const int RefreshCost = 1000;
        private bool _isRefreshing;
        /// <summary>生成和提交期间禁止重复刷新。</summary>
        public bool IsRefreshing => _isRefreshing;
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
                _notify?.ShowNotification(L10n.Get("shop.insufficientPoints", price), NotificationKind.Warning);
                return false;
            }

            _points.SpendPoints(price, "商店购买");
            _notify?.ShowNotification(L10n.Get("shop.purchased", price), NotificationKind.Success);
            return true;
        }

        /// <summary>将商品放回商店并退还其价格。</summary>
        public void Refund(ItemVM item)
        {
            int price = GetPrice(item);
            if (price <= 0)
                return;

            _points.AddPoints(price, "商店退款");
            _notify?.ShowNotification(L10n.Get("shop.refunded", price), NotificationKind.Success);
        }

        /// <summary>进入 UIScene 时免费刷新，准备失败保留已有商品。</summary>
        public void RefreshShop(GridView shopGrid)
        {
            if (shopGrid != null && !_isRefreshing)
            {
                Refresh(shopGrid, 0);
            }
        }

        /// <summary>手动刷新消费永久积分，拖拽、余额不足或处理中均拒绝。</summary>
        public bool TryRefreshShop(GridView grid, IInventoryDragState dragState)
        {
            return grid != null && grid.GridType == GridType.Shop && dragState != null &&
                dragState.DraggingItem == null && !_isRefreshing &&
                _points != null && _points.HasEnoughPoints(RefreshCost) && Refresh(grid, RefreshCost);
        }

        /// <summary>按目录权重生成并验证布局，完整视图准备成功后才授权扣费及替换。</summary>
        private bool Refresh(GridView grid, int cost)
        {
            if (_catalog == null || _catalog.Items == null || _catalog.Items.Count == 0)
            {
                return false;
            }

            _isRefreshing = true;
            try
            {
                grid.EnsureGridVM();
                int attemptLimit = Mathf.Max(1, fillPassLimit) * Mathf.Max(1, _catalog.Items.Count);
                var generator = new ShopPlacementGenerator();
                var placements = generator.Generate(
                    _catalog.Items,
                    grid.ShapeSet,
                    grid.GridVM.Width,
                    grid.GridVM.Height,
                    _catalog.GetShopAreaWeight,
                    attemptLimit);

                var validation = new GridVM(grid.GridVM.Width, grid.GridVM.Height, GridType.Shop);
                var snapshots = new List<InventoryItemSnapshot>(placements.Count);
                for (int i = 0; i < placements.Count; i++)
                {
                    var placement = placements[i];
                    var item = new ItemVM(placement.Definition, grid.ShapeSet);
                    item.SetDirection(placement.Direction);
                    if (!validation.Place(item, placement.Column, placement.Row))
                    {
                        return false;
                    }
                    snapshots.Add(new InventoryItemSnapshot(item));
                }
                return grid.TryReplaceItems(snapshots,
                    () => cost == 0 || _points.SpendPoints(cost, "商店刷新"));
            }
            finally
            {
                _isRefreshing = false;
            }
        }
    }
}
