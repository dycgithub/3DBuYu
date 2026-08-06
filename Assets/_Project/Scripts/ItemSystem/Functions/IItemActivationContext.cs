using System.Collections.Generic;
using ShootingSystem.Buffs;

namespace ItemSystem.Functions
{
    /// <summary>
    /// 物品激活上下文：功能实现（弹药/技能）通过它执行游戏内操作。
    ///   - 弹药（Ammunition）走 ApplyAmmunitionBuffs，与 buff 系统结合。
    ///   - 技能（Skill）走 KillAllEnemies / UnlockAllPorts 等具体效果。
    /// 由游戏侧（如物品使用流程 / 玩家控制器）实现并提供给功能调用。
    /// </summary>
    public interface IItemActivationContext
    {
        /// <summary>将一组 buff 施加到玩家炮台（弹药型物品的生效入口）。</summary>
        void ApplyAmmunitionBuffs(IReadOnlyList<BuffConfig> buffs);

        /// <summary>技能效果：消灭场上所有敌人。</summary>
        void KillAllEnemies();

        /// <summary>技能效果：解锁所有炮台插槽。</summary>
        void UnlockAllPorts();
    }
}
