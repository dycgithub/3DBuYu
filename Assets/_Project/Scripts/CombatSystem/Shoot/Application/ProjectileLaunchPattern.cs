using UnityEngine;

namespace CombatSystem
{
    /// <summary>计算批量投射物的扇形方向与单发速度变化。</summary>
    internal static class ProjectileLaunchPattern
    {
        /// <summary>按批次序号生成对称扇形方向。</summary>
        internal static Vector3 GetDirection(
            Vector3 direction,
            int projectileCount,
            int projectileIndex,
            float maximumAngleDegrees)
        {
            Vector3 normalizedDirection = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector3.forward;
            if (projectileCount <= 1 || maximumAngleDegrees <= 0f)
            {
                return normalizedDirection;
            }

            float centeredIndex = projectileIndex / (projectileCount - 1f) - 0.5f;
            float angle = centeredIndex * 2f * Mathf.Clamp(maximumAngleDegrees, 0f, 180f);
            Vector3 rotationAxis = Mathf.Abs(Vector3.Dot(normalizedDirection, Vector3.up)) > 0.99f
                ? Vector3.right
                : Vector3.up;
            return Quaternion.AngleAxis(angle, rotationAxis) * normalizedDirection;
        }

        /// <summary>围绕瞄准方向在限定角度内生成一个随机方向。</summary>
        internal static Vector3 GetRandomDirection(Vector3 direction, float maximumAngleDegrees)
        {
            Vector3 normalizedDirection = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector3.forward;
            float maximumAngle = Mathf.Max(0f, maximumAngleDegrees);
            float angle = Random.Range(-maximumAngle, maximumAngle);
            Vector3 rotationAxis = Mathf.Abs(Vector3.Dot(normalizedDirection, Vector3.up)) > 0.99f
                ? Vector3.right
                : Vector3.up;
            return (Quaternion.AngleAxis(angle, rotationAxis) * normalizedDirection).normalized;
        }

        /// <summary>对单颗投射物应用一次性、对称的速度随机变化。</summary>
        internal static float GetSpeed(float speed, float variance)
        {
            float clampedVariance = Mathf.Clamp(variance, 0f, 0.5f);
            if (clampedVariance <= 0f)
            {
                return speed;
            }

            return speed * UnityEngine.Random.Range(1f - clampedVariance, 1f + clampedVariance);
        }
    }
}
