using UnityEngine;

namespace Interfaces
{
    /// <summary>
    /// 敌人运动效果的窄接缝。
    /// 调用方只提交运动命令，不需要知道敌人是否由 ECS 驱动。
    /// </summary>
    public interface IEnemyMotionEffectTarget
    {
        /// <summary>提交当前运动速度倍率。</summary>
        /// <param name="multiplier">相对基础速度的倍率。</param>
        void SetMovementSpeedMultiplier(float multiplier);

        /// <summary>提交一次外部速度冲量。</summary>
        /// <param name="impulse">单位为世界单位/秒的冲量。</param>
        void AddExternalImpulse(Vector3 impulse);
    }

    /// <summary>
    /// 支持按来源维护永久移动效果的敌人运动目标。
    /// 普通运动目标仍可通过 IEnemyMotionEffectTarget 接收一次性命令。
    /// </summary>
    public interface IPermanentEnemyMotionEffectTarget
    {
        /// <summary>设置指定来源的永久速度倍率。</summary>
        void SetPermanentMovementSpeedMultiplier(float multiplier, int sourceInstanceId);

        /// <summary>移除指定来源的永久速度倍率。</summary>
        void ClearPermanentMovementSpeedMultiplier(int sourceInstanceId);
    }
}
