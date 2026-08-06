using System;
using System.Collections.Generic;
using GameSystem;
using InventorySystem;
using ItemSystem;
using SaveSystem = GameSystem.SaveSystem;

namespace TurretSystem
{
    /// <summary>
    /// 全局玩家装备配置(纯 C# 单例,随全局容器永生)。
    /// TurretInventory(炮塔装备格)+ PortInventory 列表(炮口装备格)。
    /// 战斗时 Turret/TurretPortManager 直接消费本实例,基地阶段背包同样绑定本实例。
    /// </summary>
    public class PlayerLoadout
    {
        public TurretInventory TurretInventory { get; }
        public IReadOnlyList<PortInventory> PortInventories { get; }

        private readonly ItemConfigRegistry _registry;
        private readonly TurretBase _turretBase;

        public PlayerLoadout(ItemConfigRegistry registry, TurretBase turretBase)
        {
            if (turretBase == null) throw new ArgumentNullException(nameof(turretBase));

            _registry = registry;
            _turretBase = turretBase;
            TurretInventory = new TurretInventory(
                null,
                turretBase.turretInventoryColumns,
                turretBase.turretInventoryRows,
                turretBase.detectionRadius,
                turretBase.baseRotationSpeed);

            var settings = PortInventorySettings.CreateDefault();
            var ports = new List<PortInventory>();
            if (turretBase.firingPorts != null)
            {
                for (int i = 0; i < turretBase.firingPorts.Length; i++)
                    ports.Add(new PortInventory(settings, i));
            }
            PortInventories = ports;

            Load();
        }

        public void Load()
        {
            var data = SaveSystem.LoadInventoryData();
            if (data?.items == null) return;

            var turretItems = new List<PlacedItem>();
            var portItems = new Dictionary<int, List<PlacedItem>>();

            foreach (var o in data.items)
            {
                if (o.label == SlotLabel.TurretBag.ToString())
                {
                    turretItems.Add(ToPlaced(o));
                }
                else if (o.label.StartsWith(SlotLabel.PortBag.ToString() + "_"))
                {
                    string suffix = o.label.Substring((SlotLabel.PortBag.ToString() + "_").Length);
                    if (int.TryParse(suffix, out int idx))
                    {
                        if (!portItems.ContainsKey(idx)) portItems[idx] = new List<PlacedItem>();
                        portItems[idx].Add(ToPlaced(o));
                    }
                }
            }

            TurretInventory.Grid.LoadFromData(turretItems.ToArray(), _registry);
            TurretInventory.Attributes.Recalculate(TurretInventory.Grid.GetAllItems(), TurretInventory.Grid);

            for (int i = 0; i < PortInventories.Count; i++)
            {
                if (portItems.TryGetValue(i, out var list))
                    PortInventories[i].Grid.LoadFromData(list.ToArray(), _registry);
            }
        }

        public void Save()
        {
            var items = new List<OwnedItem>();
            foreach (var p in TurretInventory.Grid.ToSaveData())
                items.Add(ToOwned(p, SlotLabel.TurretBag.ToString()));

            for (int i = 0; i < PortInventories.Count; i++)
            {
                foreach (var p in PortInventories[i].Grid.ToSaveData())
                    items.Add(ToOwned(p, $"{SlotLabel.PortBag}_{i}"));
            }

            InventorySaveHelper.SaveMerged(o =>
                o.label == SlotLabel.TurretBag.ToString() ||
                o.label.StartsWith(SlotLabel.PortBag.ToString() + "_"), items);
        }

        /// <summary>
        /// 装备结算后清空全部装备格(物品已按原价折算积分)。
        /// </summary>
        public void ClearEquipment()
        {
            TurretInventory.Grid.Clear();
            TurretInventory.Attributes.Recalculate(TurretInventory.Grid.GetAllItems(), TurretInventory.Grid);
            foreach (var port in PortInventories)
                port.Grid.Clear();
        }

        /// <summary>端口是否锁定(来自 TurretBase 配置)。</summary>
        public bool IsPortLocked(int index)
            => _turretBase.firingPorts != null &&
               index >= 0 && index < _turretBase.firingPorts.Length &&
               _turretBase.firingPorts[index].isInitiallyLocked;

        private static PlacedItem ToPlaced(OwnedItem o)
            => new PlacedItem { itemConfigId = o.itemConfigId, row = o.row, col = o.col, rotation = o.rotation };

        private static OwnedItem ToOwned(PlacedItem p, string label)
            => new OwnedItem { itemConfigId = p.itemConfigId, label = label, row = p.row, col = p.col, rotation = p.rotation };
    }
}
