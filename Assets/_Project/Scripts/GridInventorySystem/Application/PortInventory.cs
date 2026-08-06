using System;
using InventorySystem;
using ItemSystem;
using UnityEngine;

namespace TurretSystem
{
    /// <summary>
    /// 端口背包设置。
    /// </summary>
    [Serializable]
    public class PortInventorySettings
    {
        public int gridWidth = 3;
        public int gridHeight = 3;

        public static PortInventorySettings CreateDefault()
        {
            return new PortInventorySettings
            {
                gridWidth = 3,
                gridHeight = 3
            };
        }
    }

    /// <summary>
    /// 端口独立网格背包。继承 BaseInventory。
    /// </summary>
    public class PortInventory : BaseInventory
    {
        /// <summary>端口索引（用于 UI 标识）。</summary>
        public int PortIndex { get; }

        public PortInventory(PortInventorySettings settings, int portIndex)
            : base(
                settings?.gridWidth ?? 3,
                settings?.gridHeight ?? 3,
                new ItemTypeValidator(ItemType.Ammunition),
                new PortAttributes())
        {
            PortIndex = portIndex;
        }

        public PortInventory(PortInventorySettings settings, int portIndex, PortAttributes templateAttributes)
            : base(
                settings?.gridWidth ?? 3,
                settings?.gridHeight ?? 3,
                new ItemTypeValidator(ItemType.Ammunition),
                templateAttributes != null ? new PortAttributes(templateAttributes) : new PortAttributes())
        {
            PortIndex = portIndex;
        }

        /// <summary>PortAttributes 类型转换。</summary>
        public new PortAttributes Attributes => (PortAttributes)base.Attributes;
    }
}
