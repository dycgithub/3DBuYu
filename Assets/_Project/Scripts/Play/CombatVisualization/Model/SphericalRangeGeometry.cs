using UnityEngine;

namespace Play.CombatVisualization.Model
{
    /// <summary>生成运行时球面范围线所需的闭合圆环点位。</summary>
    public static class SphericalRangeGeometry
    {
        private const float MinimumVectorSqrMagnitude = 0.000001f;

        /// <summary>
        /// 将圆环点位写入预分配数组。
        /// </summary>
        /// <param name="center">圆环中心。</param>
        /// <param name="radius">圆环半径；非法值按零处理。</param>
        /// <param name="axisA">圆环所在平面的第一条轴。</param>
        /// <param name="axisB">圆环所在平面的第二条轴。</param>
        /// <param name="points">用于接收点位的数组，首尾点会重合。</param>
        public static void FillCircle(
            Vector3 center,
            float radius,
            Vector3 axisA,
            Vector3 axisB,
            Vector3[] points)
        {
            if (points == null || points.Length == 0)
            {
                return;
            }

            float safeRadius = IsFinite(radius) ? Mathf.Max(0f, radius) : 0f;
            Vector3 firstAxis = NormalizeAxis(axisA, Vector3.right);
            Vector3 secondAxis = axisB - Vector3.Project(axisB, firstAxis);
            secondAxis = NormalizeAxis(secondAxis, FindPerpendicular(firstAxis));

            int segmentCount = Mathf.Max(1, points.Length - 1);
            for (int index = 0; index < points.Length; index++)
            {
                if (index == segmentCount)
                {
                    points[index] = points[0];
                    continue;
                }

                float angle = index / (float)segmentCount * Mathf.PI * 2f;
                Vector3 direction = firstAxis * Mathf.Cos(angle) +
                    secondAxis * Mathf.Sin(angle);
                points[index] = center + direction * safeRadius;
            }
        }

        /// <summary>返回有限且归一化的轴向量。</summary>
        private static Vector3 NormalizeAxis(Vector3 value, Vector3 fallback)
        {
            Vector3 axis = value.sqrMagnitude >= MinimumVectorSqrMagnitude
                ? value.normalized
                : fallback.normalized;
            return axis.sqrMagnitude >= MinimumVectorSqrMagnitude
                ? axis
                : Vector3.right;
        }

        /// <summary>为指定轴找到一条稳定的垂直轴。</summary>
        private static Vector3 FindPerpendicular(Vector3 axis)
        {
            Vector3 reference = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) < 0.9f
                ? Vector3.up
                : Vector3.right;
            Vector3 perpendicular = Vector3.Cross(axis, reference);
            return perpendicular.sqrMagnitude >= MinimumVectorSqrMagnitude
                ? perpendicular.normalized
                : Vector3.forward;
        }

        /// <summary>判断浮点数是否可用于几何计算。</summary>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
