using System.Collections.Generic;
using UnityEngine;

namespace TurretSystem
{
    /// <summary>
    /// 球冠炮台端口布局策略。
    /// Turret 位于球冠顶点（星球表面），port 均匀分布在球冠底面圆周上。
    /// </summary>
    public static class TurretCapLayout
    {
        /// <summary>
        /// 端口名称到圆周角度的映射（0°=右，逆时针递增）。
        /// </summary>
        public static readonly Dictionary<string, float> PortNameToAngle = new()
        {
            { "右", 0f },       { "Right", 0f },
            { "右上", 45f },    { "TopRight", 45f },
            { "上", 90f },      { "Top", 90f },
            { "左上", 135f },   { "TopLeft", 135f },
            { "左", 180f },     { "Left", 180f },
            { "左下", 225f },   { "BottomLeft", 225f },
            { "下", 270f },     { "Bottom", 270f },
            { "右下", 315f },   { "BottomRight", 315f },
        };

        /// <summary>
        /// 计算单个 port 的世界空间位置(在球冠底面圆周上)。
        /// 旋转由 PortAimController 统一控制,不在此处设置。
        /// </summary>
        /// <param name="sphereCenter">星球中心</param>
        /// <param name="turretPos">Turret 位置（球冠顶点）</param>
        /// <param name="capHeight">球冠高度</param>
        /// <param name="portName">端口方向名称</param>
        /// <param name="position">输出端口位置</param>
        public static void CalculatePortPose(
            Vector3 sphereCenter,
            Vector3 turretPos,
            float capHeight,
            string portName,
            out Vector3 position)
        {
            Vector3 normal = (turretPos - sphereCenter).normalized;
            float R = Vector3.Distance(turretPos, sphereCenter);
            float h = Mathf.Clamp(capHeight, 0f, R);
            float a = Mathf.Sqrt(2f * R * h - h * h);
            Vector3 capBaseCenter = sphereCenter + normal * (R - h);

            SphericalCoordinates.GetTangentBasis(normal, out Vector3 east, out Vector3 north);

            float angleDeg = PortNameToAngle.GetValueOrDefault(portName, 0f);
            float angleRad = angleDeg * Mathf.Deg2Rad;
            Vector3 dir = (east * Mathf.Cos(angleRad) + north * Mathf.Sin(angleRad)).normalized;

            position = capBaseCenter + dir * a;
        }

        /// <summary>
        /// 获取球冠底面圆周上的采样点，用于编辑器绘制。
        /// </summary>
        /// <param name="sphereCenter">星球中心</param>
        /// <param name="turretPos">Turret 位置（球冠顶点）</param>
        /// <param name="capHeight">球冠高度</param>
        /// <param name="segments">采样段数</param>
        /// <returns>底面圆周上的点数组</returns>
        public static Vector3[] GetCapBaseCircle(
            Vector3 sphereCenter,
            Vector3 turretPos,
            float capHeight,
            int segments = 32)
        {
            Vector3 normal = (turretPos - sphereCenter).normalized;
            float R = Vector3.Distance(turretPos, sphereCenter);
            float h = Mathf.Clamp(capHeight, 0f, R);
            float a = Mathf.Sqrt(2f * R * h - h * h);
            Vector3 capBaseCenter = sphereCenter + normal * (R - h);

            SphericalCoordinates.GetTangentBasis(normal, out Vector3 east, out Vector3 north);

            Vector3[] points = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                Vector3 dir = (east * Mathf.Cos(angle) + north * Mathf.Sin(angle)).normalized;
                points[i] = capBaseCenter + dir * a;
            }
            return points;
        }
    }
}
