using System.Collections.Generic;
using CombatSystem;

namespace _Project.Tests.CombatPlayground.Implement
{
    /// <summary>为技能释放实验提供可重置的合成物品实例。</summary>
    public sealed class CombatPlaygroundInventory : ICombatItemConsumptionService
    {
        private readonly HashSet<int> _items = new();

        /// <summary>登记一个场景内技能卡来源。</summary>
        internal void RegisterItem(int itemInstanceId)
        {
            _items.Add(itemInstanceId);
        }

        /// <summary>清空上一轮实验的合成物品。</summary>
        internal void Clear()
        {
            _items.Clear();
        }

        /// <summary>检查技能来源是否仍可消费。</summary>
        public bool CanConsumeItemInstance(int itemInstanceId)
        {
            return _items.Contains(itemInstanceId);
        }

        /// <summary>消费最后一次使用对应的场景合成物品。</summary>
        public bool TryConsumeItemInstance(int itemInstanceId)
        {
            return _items.Remove(itemInstanceId);
        }
    }
}
