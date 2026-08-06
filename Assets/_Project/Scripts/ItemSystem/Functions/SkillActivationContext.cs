using System.Collections.Generic;
using Services;
using ShootingSystem.Buffs;
using TurretSystem;

namespace ItemSystem.Functions
{
    /// <summary>
    /// 技能激活上下文 — 命令模式 Receiver。
    /// 技能命令(ISkill)通过它执行具体游戏内操作:
    ///   - KillAllEnemies → 消灭场上所有敌人(走完整死亡链路)
    ///   - UnlockAllPorts → 依次解锁所有锁定炮口
    ///   - ApplyAmmunitionBuffs → 预留(弹药已由 PortFireController 弹种链路直接生效)
    /// 由 GameLoopLifetimeScope 以单例注册,注入战斗场景服务。
    /// </summary>
    public class SkillActivationContext : IItemActivationContext
    {
        private readonly IEnemySpawner _enemySpawner;
        private readonly Turret _turret;

        public SkillActivationContext(IEnemySpawner enemySpawner, Turret turret)
        {
            _enemySpawner = enemySpawner;
            _turret = turret;
        }

        /// <summary>弹药 buff 入口:第一版不启用(弹药效果走 providedBulletConfig + 属性加成),预留扩展。</summary>
        public void ApplyAmmunitionBuffs(IReadOnlyList<BuffConfig> buffs)
        {
        }

        public void KillAllEnemies()
        {
            _enemySpawner?.KillAllEnemies();
        }

        public void UnlockAllPorts()
        {
            if (_turret == null) return;
            while (_turret.TryExpandPort() != null) { }
        }
    }
}
