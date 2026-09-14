using System.Collections.Generic;
using Interfaces;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 为中央主动技能提供场景效果的窄运行时接缝。
    /// 动作资产只依赖该接口，不查找场景对象，也不持有运行时状态。
    /// </summary>
    public interface ICentralSkillEffectService
    {
        /// <summary>尝试解锁中央的下一个发射器端口。</summary>
        bool TryUnlockNextTransmitter();

        /// <summary>向目标快照中的敌人提交远离施法位置的击退冲量。</summary>
        bool TryPushEnemies(
            IReadOnlyList<IDamageable> targets,
            Vector3 sourcePosition,
            float impulseStrength);

        /// <summary>向目标快照中的敌人提交永久速度倍率。</summary>
        bool TrySlowEnemies(
            IReadOnlyList<IDamageable> targets,
            float multiplier,
            int sourceInstanceId);

        /// <summary>向发射器全局效果边界提交射速倍率。</summary>
        bool TrySetTransmitterAttackSpeedMultiplier(
            float multiplier,
            int sourceInstanceId);
    }
}
