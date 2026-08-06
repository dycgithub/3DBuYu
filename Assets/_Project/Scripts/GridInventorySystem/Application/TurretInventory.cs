using InventorySystem;
using ItemSystem;

namespace TurretSystem
{
    /// <summary>
    /// 炮台级网格背包。继承 BaseInventory，仅限制物品类型为 TurretModule。
    /// </summary>
    public class TurretInventory : BaseInventory
    {
        public Turret Owner { get; }

        public TurretInventory(Turret owner, int columns, int rows,
            float baseDetectionRadius, float baseRotationSpeed)
            : base(columns, rows, new ItemTypeValidator(ItemType.Skill),
                new TurretAttributes(baseDetectionRadius, baseRotationSpeed))
        {
            Owner = owner;
        }

        /// <summary>TurretAttributes 类型转换。</summary>
        public new TurretAttributes Attributes => (TurretAttributes)base.Attributes;

        /// <summary>获取属性描述（用于 UI）。</summary>
        public string GetAttributesDescription() => Attributes.GetDescription();
    }
}
