using SphereMovement.Interfaces;
using UnityEngine;

namespace SphereMovement.Environment
{
    /// <summary>
    /// 球面环境组件
    /// 定义球面空间的属性和约束
    /// </summary>
    [AddComponentMenu("Movement/Sphere Surface")]
    public class SphereSurface : MonoBehaviour, ISurface
    {
        [Header("球体设置")]
        [Tooltip("球心位置")]
        [SerializeField] private Vector3 sphereCenter = Vector3.zero;

        [Tooltip("球半径")]
        [SerializeField] private float sphereRadius = 5f;

        [Tooltip("是否可视化球面")]
        [SerializeField] private bool visualizeSurface = true;

        [Tooltip("可视化颜色")]
        [SerializeField] private Color visualizationColor = new Color(0.5f, 0.8f, 1f, 0.3f);

        [Tooltip("可视化线框密度")]
        [SerializeField] private int visualizationSegments = 16;

        /// <summary>
        /// 球心位置
        /// </summary>
        public Vector3 Center
        {
            get => sphereCenter;
            set => sphereCenter = value;
        }

        /// <summary>
        /// 球半径
        /// </summary>
        public float Radius
        {
            get => sphereRadius;
            set => sphereRadius = Mathf.Max(0.001f, value);
        }

        /// <summary>
        /// 球表面积
        /// </summary>
        public float SurfaceArea => 4f * Mathf.PI * sphereRadius * sphereRadius;

        /// <summary>
        /// 球体积
        /// </summary>
        public float Volume => 4f / 3f * Mathf.PI * sphereRadius * sphereRadius * sphereRadius;

        private void OnValidate()
        {
            sphereRadius = Mathf.Max(0.001f, sphereRadius);
        }

        private void OnDrawGizmosSelected()
        {
            if (!visualizeSurface) return;

            DrawSphereWireframe(sphereCenter, sphereRadius, visualizationColor, visualizationSegments);
        }

        #region ISurface Implementation

        public Vector3 GetNormalAtPosition(Vector3 position)
        {
            return (position - sphereCenter).normalized;
        }

        public Vector3 GetTangentAtPosition(Vector3 position, Vector3 directionHint)
        {
            Vector3 normal = GetNormalAtPosition(position);
            Vector3 tangent = Vector3.ProjectOnPlane(directionHint, normal).normalized;

            if (tangent.sqrMagnitude < 0.001f)
            {
                // 如果方向与法线平行，选择一个默认切线
                tangent = Vector3.ProjectOnPlane(Vector3.forward, normal).normalized;
                if (tangent.sqrMagnitude < 0.001f)
                {
                    tangent = Vector3.ProjectOnPlane(Vector3.right, normal).normalized;
                }
            }

            return tangent;
        }

        public Vector3 ClampPositionToSurface(Vector3 position)
        {
            Vector3 direction = position - sphereCenter;
            return sphereCenter + direction.normalized * sphereRadius;
        }

        public Vector3 GetClosestPointOnSurface(Vector3 worldPosition)
        {
            return ClampPositionToSurface(worldPosition);
        }

        public bool IsPointOnSurface(Vector3 position, float tolerance = 0.01f)
        {
            float distanceToCenter = Vector3.Distance(position, sphereCenter);
            return Mathf.Abs(distanceToCenter - sphereRadius) <= tolerance;
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// 球坐标转世界坐标
        /// </summary>
        public Vector3 SphericalToWorld(float longitude, float latitude)
        {
            float cosLat = Mathf.Cos(latitude);
            float x = cosLat * Mathf.Sin(longitude);
            float y = Mathf.Sin(latitude);
            float z = cosLat * Mathf.Cos(longitude);

            return sphereCenter + new Vector3(x, y, z) * sphereRadius;
        }

        /// <summary>
        /// 世界坐标转球坐标
        /// </summary>
        public Vector2 WorldToSpherical(Vector3 worldPosition)
        {
            Vector3 localPos = (worldPosition - sphereCenter).normalized;
            float longitude = Mathf.Atan2(localPos.x, localPos.z);
            float latitude = Mathf.Asin(localPos.y);
            return new Vector2(longitude, latitude);
        }

        /// <summary>
        /// 获取球面上两点的最短距离（大圆距离）
        /// </summary>
        public float GetGreatCircleDistance(Vector3 pointA, Vector3 pointB)
        {
            Vector3 a = (pointA - sphereCenter).normalized;
            Vector3 b = (pointB - sphereCenter).normalized;
            float dot = Mathf.Clamp(Vector3.Dot(a, b), -1f, 1f);
            float angle = Mathf.Acos(dot);
            return angle * sphereRadius;
        }

        #endregion

        #region Visualization

        private void DrawSphereWireframe(Vector3 center, float radius, Color color, int segments)
        {
            Gizmos.color = color;

            // 绘制纬线
            for (int i = 1; i < segments; i++)
            {
                float t = (float)i / segments;
                float latitude = Mathf.Lerp(-Mathf.PI / 2f, Mathf.PI / 2f, t);
                DrawLatitudeCircle(center, radius, latitude, segments);
            }

            // 绘制经线
            for (int i = 0; i < segments; i++)
            {
                float longitude = Mathf.Lerp(0f, Mathf.PI * 2f, (float)i / segments);
                DrawLongitudeArc(center, radius, longitude, segments);
            }

            // 绘制赤道
            DrawLatitudeCircle(center, radius, 0f, segments * 2);
        }

        private void DrawLatitudeCircle(Vector3 center, float radius, float latitude, int segments)
        {
            float cosLat = Mathf.Cos(latitude);
            float sinLat = Mathf.Sin(latitude);
            float circleRadius = radius * cosLat;
            float height = radius * sinLat;
            Vector3 circleCenter = center + Vector3.up * height;

            Vector3 prevPoint = Vector3.zero;
            bool first = true;

            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                Vector3 point = circleCenter + new Vector3(
                    Mathf.Sin(angle) * circleRadius,
                    0f,
                    Mathf.Cos(angle) * circleRadius
                );

                if (!first)
                {
                    Gizmos.DrawLine(prevPoint, point);
                }

                prevPoint = point;
                first = false;
            }
        }

        private void DrawLongitudeArc(Vector3 center, float radius, float longitude, int segments)
        {
            Vector3 prevPoint = Vector3.zero;
            bool first = true;

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float latitude = Mathf.Lerp(-Mathf.PI / 2f, Mathf.PI / 2f, t);

                Vector3 point = center + SphericalToWorld(longitude, latitude) * radius;

                if (!first)
                {
                    Gizmos.DrawLine(prevPoint, point);
                }

                prevPoint = point;
                first = false;
            }
        }

        #endregion
    }
}
