using System.Threading;

namespace CombatSystem
{
    /// <summary>一个物品在本局中的运行时实例，负责实例 ID 和耐久。</summary>
    public sealed class ItemInstance
    {
        private static int _nextInstanceId;

        public int InstanceId { get; }
        public ItemDefinition Definition { get; }
        public float CurrentDurability { get; private set; }

        public CombatItemGrant CombatGrant => Definition != null ? Definition.CombatGrant : null;

        public ItemInstance(ItemDefinition definition, float durability = 0f)
        {
            InstanceId = Interlocked.Increment(ref _nextInstanceId);
            Definition = definition;
            CurrentDurability = durability;
        }

        public bool TrySpendDurability(float amount)
        {
            if (amount <= 0f)
                return true;
            if (CurrentDurability < amount)
                return false;

            CurrentDurability -= amount;
            return true;
        }
    }
}
