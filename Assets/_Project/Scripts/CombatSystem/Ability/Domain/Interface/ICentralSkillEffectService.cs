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
        bool TrySetTransmitterSpeed(
            float multiplier,
            int sourceInstanceId,
            float durationSeconds);

        /// <summary>对目标快照应用一到两个限时 Buff。</summary>
        int ApplyBuffs(
            IReadOnlyList<IDamageable> targets,
            BuffDefinitionSO primary,
            BuffDefinitionSO secondary,
            int sourceInstanceId);

        /// <summary>将发射器目标短暂切换为当前生命最高的敌人。</summary>
        bool TryFocusHighestHealth(
            IReadOnlyList<IDamageable> targets,
            float durationSeconds,
            int sourceInstanceId);
    }
}
