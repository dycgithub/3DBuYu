using System;
using ItemSystem;
using Services;
using UnityEngine;
using VContainer;

namespace InventorySystem.Shop
{
    /// <summary>商店购买结果。</summary>
    public enum PurchaseResult
    {
        Success,
        NoTarget,
        ShapeBlocked,
        NotEnoughPoints,
        ItemMissing,
        TransferFailed,
    }

    /// <summary>
    /// 商店管理器(场景组件,注册于 ProjectLifetimeScope)。
    /// 持有商店货架 ShopInventory;购买/货架刷新/售价查询统一由商店负责(与 ItemConfig 解耦)。
    /// </summary>
    public class ShopManager : MonoBehaviour
    {
        [SerializeField] private ShopConfig shopConfig;
        [SerializeField] private int columns = 9;
        [SerializeField] private int rows = 9;

        [Inject] private IPointsService _points;

        /// <summary>商店货架网格。</summary>
        public ShopInventory Stock { get; private set; }

        private void Awake()
        {
            Stock = new ShopInventory(this, columns, rows);
        }

        /// <summary>
        /// 从货架购买商品到目标网格(带旋转)。
        /// 流程:商品存在 → 目标可放置 → 扣积分 → 落网格 → 货架移除。
        /// </summary>
        public PurchaseResult? TryPurchaseToSlot(int instanceId, InventoryGrid targetGrid, int row, int col, int rotation)
        {
            if (Stock == null || Stock.Grid == null || targetGrid == null)
                return PurchaseResult.NoTarget;

            var config = Stock.Grid.GetItemConfig(instanceId);
            if (config == null || config.shape == null)
                return PurchaseResult.ItemMissing;

            if (!targetGrid.CanPlaceAt(config, row, col, rotation))
                return PurchaseResult.ShapeBlocked;

            int price = shopConfig != null ? shopConfig.GetBasePrice(config.itemId) : 0;
            if (price > 0 && !SpendPoints(price, config))
                return PurchaseResult.NotEnoughPoints;

            int newId = targetGrid.PlaceItem(config, row, col, rotation);
            if (newId < 0)
                return PurchaseResult.TransferFailed;

            if (!Stock.RemoveItem(instanceId))
            {
                // 回滚落点
                targetGrid.RemoveItem(newId);
                return PurchaseResult.TransferFailed;
            }

            return PurchaseResult.Success;
        }

        /// <summary>货架内移动(免费,不扣积分)。</summary>
        public bool MoveOnStock(int instanceId, int row, int col, int rotation)
            => Stock != null && Stock.MoveItem(instanceId, row, col, rotation);

        /// <summary>按价格表重建货架商品(PlaceRandomly 随机落格)。</summary>
        public void RefreshStock(ItemConfigRegistry registry)
        {
            if (Stock == null || shopConfig == null || registry == null) return;

            Stock.Grid.Clear();
            foreach (var p in shopConfig.prices)
            {
                if (registry.TryGet(p.itemId, out var config))
                    Stock.PlaceRandomly(config);
            }
        }

        private bool SpendPoints(int price, ItemConfig config)
        {
            if (_points == null) return false;
            return _points.SpendPoints(price, $"购买 {config.displayName}");
        }

        public static string GetErrorMessage(PurchaseResult result) => result switch
        {
            PurchaseResult.ShapeBlocked => "这里放不下！",
            PurchaseResult.NotEnoughPoints => "积分不足！",
            PurchaseResult.ItemMissing => "商品不存在",
            PurchaseResult.NoTarget => "目标背包不可用",
            PurchaseResult.TransferFailed => "购买失败",
            _ => "未知错误",
        };
    }
}
