using Interfaces;
using ItemSystem;

namespace InventorySystem
{
    /// <summary>接受所有物品（玩家通用仓库）。</summary>
    public sealed class AnyItemValidator : IInventoryValidator
    {
        public bool CanAccept(ItemConfig config) => config != null;
    }

    /// <summary>仅接受指定类型的物品（炮塔收 Skill、端口收 Ammunition）。</summary>
    public sealed class ItemTypeValidator : IInventoryValidator
    {
        private readonly ItemType _allowedType;

        public ItemTypeValidator(ItemType allowedType)
        {
            _allowedType = allowedType;
        }

        public bool CanAccept(ItemConfig config)
            => config != null && config.ItemType == _allowedType;
    }
}
