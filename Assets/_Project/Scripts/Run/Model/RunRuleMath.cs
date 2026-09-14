using System;
using UnityEngine;

namespace GameSystem
{
    public static class RunRuleMath
    {
        /// <summary>
        /// 判断本局是否同时满足时间和击杀要求。
        /// </summary>
        /// <param name="elapsedTime">本局已运行时间。</param>
        /// <param name="targetDuration">要求达到的目标时长。</param>
        /// <param name="killCount">本局已完成的击杀数。</param>
        /// <param name="targetKillCount">要求达到的击杀数。</param>
        /// <returns>满足所有要求时返回 true，否则返回 false。</returns>
        public static bool MeetsRunRequirements(
            float elapsedTime,
            float targetDuration,
            int killCount,
            int targetKillCount)
        {
            if (!IsFinite(elapsedTime))
            {
                return false;
            }

            float safeTargetDuration = IsFinite(targetDuration) ? Mathf.Max(0f, targetDuration) : 0f;
            if (elapsedTime < safeTargetDuration)
            {
                return false;
            }

            return targetKillCount <= 0 || killCount >= targetKillCount;
        }

        /// <summary>
        /// 计算本局结算奖励积分。
        /// </summary>
        /// <param name="sessionPoints">本局积分。</param>
        /// <param name="settlementMultiplier">难度结算倍率。</param>
        /// <returns>结算奖励积分。</returns>
        public static int CalculateSettlementReward(
            int sessionPoints,
            float settlementMultiplier)
        {
            if (sessionPoints <= 0 ||
                !IsPositiveFinite(settlementMultiplier))
            {
                return 0;
            }

            double reward = (double)sessionPoints * settlementMultiplier;
            if (double.IsNaN(reward) || reward <= 0d)
            {
                return 0;
            }

            if (double.IsInfinity(reward) || reward >= int.MaxValue)
            {
                return int.MaxValue;
            }

            return (int)Math.Floor(reward);
        }

        /// <summary>
        /// 判断浮点数是否为有限值。
        /// </summary>
        /// <param name="value">要检查的浮点数。</param>
        /// <returns>值不是 NaN 或无穷大时返回 true。</returns>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool IsPositiveFinite(float value)
        {
            return IsFinite(value) && value > 0f;
        }
    }
}
