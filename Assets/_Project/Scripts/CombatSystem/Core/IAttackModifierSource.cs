using System.Collections.Generic;

namespace CombatSystem
{
    /// <summary>向攻击系统提供指定端口的攻击修改器。</summary>
    public interface IAttackModifierSource
    {
        void CollectAttackModifiers(
            int portIndex,
            List<IAttackModifier> destination);
    }
}
