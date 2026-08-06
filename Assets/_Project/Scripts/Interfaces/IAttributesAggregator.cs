using System;
using System.Collections.Generic;
using InventorySystem;

namespace Interfaces
{
    /// <summary>
    /// 属性聚合器接口。
    /// 由装备槽位的物品列表驱动，重新计算炮口/炮台的最终战斗属性。
    /// PortAttributes、TurretAttributes 实现此接口。
    /// </summary>
    public interface IAttributesAggregator
    {
        /// <summary>属性变化时触发。</summary>
        event Action OnAttributesChanged;

        /// <summary>从网格中的物品重新计算聚合属性。</summary>
        /// <param name="items">已放置的物品列表。</param>
        /// <param name="grid">提供物品配置查找的网格。</param>
        void Recalculate(IReadOnlyList<PlacedItem> items, InventoryGrid grid);

        /// <summary>重置为基础值。</summary>
        void ResetToBase();

        /// <summary>获取属性描述（用于 UI）。</summary>
        string GetDescription();
    }
}
