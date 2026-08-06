using System;
using UnityEngine;

namespace InventorySystem.Shop
{
    /// <summary>
    /// 商店经济配置(ScriptableObject):物品售价由商店负责,与 ItemConfig 解耦。
    /// 含基础价/售价/维修费,供购买、装备结算、修理查询。
    /// </summary>
    [CreateAssetMenu(menuName = "Inventory/Shop Config")]
    public class ShopConfig : ScriptableObject
    {
        [Serializable]
        public struct ItemPrice
        {
            public string itemId;
            public int basePrice;
            public int sellPrice;
            public int repairCost;
        }

        [Tooltip("商品价格表(按 itemId 索引)")]
        public ItemPrice[] prices = Array.Empty<ItemPrice>();

        /// <summary>查询基础价(购买/装备结算)。无配置时返回 0。</summary>
        public int GetBasePrice(string itemId)
        {
            foreach (var p in prices)
            {
                if (p.itemId == itemId) return p.basePrice;
            }
            return 0;
        }

        /// <summary>查询维修费。无配置时返回 0。</summary>
        public int GetRepairCost(string itemId)
        {
            foreach (var p in prices)
            {
                if (p.itemId == itemId) return p.repairCost;
            }
            return 0;
        }
    }
}
