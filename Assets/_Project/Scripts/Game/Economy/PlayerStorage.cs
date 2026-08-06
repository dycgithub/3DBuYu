using System;
using System.Collections.Generic;
using InventorySystem;
using ItemSystem;
using TurretSystem;

namespace GameSystem
{
    /// <summary>
    /// 全局玩家仓库(纯 C# 单例,随全局容器永生)。
    /// 商店购买落点 + 跨局持久化的 StorageInventory。
    /// </summary>
    public class PlayerStorage
    {
        public StorageInventory Inventory { get; }

        private readonly ItemConfigRegistry _registry;

        public PlayerStorage(ItemConfigRegistry registry)
        {
            _registry = registry;
            Inventory = new StorageInventory(null, 8, 8);
            Load();
        }

        public void Load()
        {
            var data = SaveSystem.LoadInventoryData();
            if (data?.items == null) return;

            var list = new List<PlacedItem>();
            foreach (var o in data.items)
            {
                if (o.label != SlotLabel.Storage.ToString()) continue;
                list.Add(ToPlaced(o));
            }
            Inventory.Grid.LoadFromData(list.ToArray(), _registry);
        }

        public void Save()
        {
            var items = new List<OwnedItem>();
            foreach (var p in Inventory.Grid.ToSaveData())
                items.Add(ToOwned(p, SlotLabel.Storage.ToString()));

            InventorySaveHelper.SaveMerged(o => o.label == SlotLabel.Storage.ToString(), items);
        }

        private static PlacedItem ToPlaced(OwnedItem o)
            => new PlacedItem { itemConfigId = o.itemConfigId, row = o.row, col = o.col, rotation = o.rotation };

        private static OwnedItem ToOwned(PlacedItem p, string label)
            => new OwnedItem { itemConfigId = p.itemConfigId, label = label, row = p.row, col = p.col, rotation = p.rotation };
    }

    /// <summary>
    /// 库存存档合并保存辅助:多个全局数据源(仓库/装备)共写 inventory.json,互不覆盖。
    /// </summary>
    internal static class InventorySaveHelper
    {
        public static void SaveMerged(Func<OwnedItem, bool> isMine, List<OwnedItem> items)
        {
            var existing = SaveSystem.LoadInventoryData();
            var kept = new List<OwnedItem>();
            if (existing?.items != null)
            {
                foreach (var o in existing.items)
                {
                    if (o == null || isMine(o)) continue;
                    kept.Add(o);
                }
            }
            kept.AddRange(items);
            SaveSystem.SaveInventoryData(new InventorySaveData { items = kept.ToArray() });
        }
    }
}
