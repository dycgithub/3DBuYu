using InventorySystem;
using ItemSystem;

namespace Interfaces
{
    /// <summary>
    /// 库存物品校验策略：决定某个物品能否放入库存。
    /// 库存的类型规则从"子类覆盖"（ValidateItem）解耦为"构造注入策略"，
    /// 便于组合复用（如"类型为 Skill 且未过期"）。
    /// </summary>
    public interface IInventoryValidator
    {
        bool CanAccept(ItemConfig config);
    }
}
