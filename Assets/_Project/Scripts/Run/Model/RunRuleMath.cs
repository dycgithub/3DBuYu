using System;
using UnityEngine;

namespace GameSystem
{
    public static class RunRuleMath
    {
        public const float DefaultOvertimeStartMultiplier = 2f;
        public const float DefaultOvertimeMultiplierIntervalSeconds = 5f;
        public const float DefaultOvertimeMultiplierIncrement = 1f;

        /// <summary>判断本局是否已经达到目标时长。</summary>
        /// <param name="elapsedTime">本局已运行时间。</param>
        /// <param name="targetDuration">目标时长。</param>
        /// <returns>达到目标时长时返回 true。</returns>
        public static bool HasReachedTargetDuration(float elapsedTime, float targetDuration)
        {
            if (!IsFinite(elapsedTime))
            {
                return false;
            }

            float safeTargetDuration = IsFinite(targetDuration) ? Mathf.Max(0f, targetDuration) : 0f;
            return elapsedTime >= safeTargetDuration;
        }

        /// <summary>判断本局是否达到配置的最低击杀数。</summary>
        /// <param name="killCount">本局击杀数。</param>
        /// <param name="targetKillCount">最低击杀数；不大于零表示不限制。</param>
        /// <returns>达到击杀要求时返回 true。</returns>
        public static bool HasRequiredKills(int killCount, int targetKillCount)
        {
            return targetKillCount <= 0 || killCount >= targetKillCount;
        }

        /// <summary>按超时持续时间计算当前能量消耗倍率。</summary>
        /// <param name="overtimeElapsedTime">超时持续时间。</param>
        /// <returns>从 2 倍开始、每 5 秒增加 1 倍的倍率。</returns>
        public static float CalculateOvertimeMultiplier(float overtimeElapsedTime)
        {
            return CalculateOvertimeMultiplier(
                overtimeElapsedTime,
                DefaultOvertimeStartMultiplier,
                DefaultOvertimeMultiplierIntervalSeconds,
                DefaultOvertimeMultiplierIncrement);
        }

        /// <summary>按配置参数计算当前超时能量消耗倍率。</summary>
        /// <param name="overtimeElapsedTime">超时持续时间。</param>
        /// <param name="startMultiplier">超时初始倍率。</param>
        /// <param name="intervalSeconds">倍率增加间隔。</param>
        /// <param name="increment">每次增加的倍率。</param>
        /// <returns>当前超时倍率。</returns>
        public static float CalculateOvertimeMultiplier(
            float overtimeElapsedTime,
            float startMultiplier,
            float intervalSeconds,
            float increment)
        {
            float safeTime = IsFinite(overtimeElapsedTime) ? Mathf.Max(0f, overtimeElapsedTime) : 0f;
            float safeStart = IsPositiveFinite(startMultiplier)
                ? startMultiplier
                : DefaultOvertimeStartMultiplier;
            float safeInterval = IsPositiveFinite(intervalSeconds)
                ? intervalSeconds
                : DefaultOvertimeMultiplierIntervalSeconds;
            float safeIncrement = IsFinite(increment) ? Mathf.Max(0f, increment) : 0f;
            return safeStart + Mathf.Floor(safeTime / safeInterval) * safeIncrement;
        }

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

            return HasRequiredKills(killCount, targetKillCount);
        }

        /// <summary>
        /// 计算普通阶段和超时阶段的结算奖励积分。
        /// </summary>
        /// <param name="basePoints">目标时长前的本局积分。</param>
        /// <param name="overtimePoints">超时阶段获得的本局积分。</param>
        /// <param name="difficultyMultiplier">难度结算倍率。</param>
        /// <param name="overtimeMultiplier">结算时的超时消耗倍率。</param>
        /// <returns>向永久积分发放的结算奖励。</returns>
        public static int CalculateSettlementReward(
            int basePoints,
            int overtimePoints,
            float difficultyMultiplier,
            float overtimeMultiplier)
        {
            if ((basePoints <= 0 && overtimePoints <= 0) ||
                !IsPositiveFinite(difficultyMultiplier) ||
                !IsPositiveFinite(overtimeMultiplier))
            {
                return 0;
            }

            double reward = (double)Math.Max(0, basePoints) * difficultyMultiplier +
                            (double)Math.Max(0, overtimePoints) * overtimeMultiplier;
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

        /// <summary>兼容没有超时积分时的普通结算计算。</summary>
        /// <param name="sessionPoints">本局积分。</param>
        /// <param name="settlementMultiplier">难度结算倍率。</param>
        /// <returns>结算奖励积分。</returns>
        public static int CalculateSettlementReward(int sessionPoints, float settlementMultiplier)
        {
            return CalculateSettlementReward(
                sessionPoints,
                0,
                settlementMultiplier,
                1f);
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
