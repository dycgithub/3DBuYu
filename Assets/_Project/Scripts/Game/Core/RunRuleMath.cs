using UnityEngine;

namespace GameSystem
{
    public static class RunRuleMath
    {
        public const float OvertimeStepSeconds = 5f;

        public static int GetOvertimeTier(float elapsedTime, float targetDuration)
        {
            if (!IsFinite(elapsedTime))
                return 0;

            float safeTargetDuration = IsFinite(targetDuration) ? Mathf.Max(0f, targetDuration) : 0f;
            float overtime = Mathf.Max(0f, elapsedTime - safeTargetDuration);
            return Mathf.FloorToInt(overtime / OvertimeStepSeconds);
        }

        public static float GetOvertimeMultiplier(float elapsedTime, float targetDuration)
        {
            return 1f + GetOvertimeTier(elapsedTime, targetDuration);
        }

        public static bool MeetsRunRequirements(
            float elapsedTime,
            float targetDuration,
            int killCount,
            int targetKillCount)
        {
            if (!IsFinite(elapsedTime))
                return false;

            float safeTargetDuration = IsFinite(targetDuration) ? Mathf.Max(0f, targetDuration) : 0f;
            if (elapsedTime < safeTargetDuration)
                return false;

            return targetKillCount <= 0 || killCount >= targetKillCount;
        }

        public static int CalculateScaledPoints(int basePoints, float multiplier)
        {
            if (basePoints <= 0 || !IsFinite(multiplier) || multiplier <= 0f)
                return 0;

            float scaledPoints = basePoints * multiplier;
            if (scaledPoints >= int.MaxValue)
                return int.MaxValue;

            return Mathf.FloorToInt(scaledPoints);
        }

        public static int CalculateSettlementReward(int sessionPoints, float settlementMultiplier)
        {
            return CalculateScaledPoints(sessionPoints, settlementMultiplier);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
