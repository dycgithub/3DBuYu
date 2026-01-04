using UnityEngine;

namespace SphereMovement
{
    /// <summary>
    /// 球坐标与笛卡尔坐标转换工具
    /// </summary>
    public static class SphericalCoordinates
    {
        /// <summary>
        /// 笛卡尔坐标转球坐标
        /// </summary>
        /// <param name="cartesian">笛卡尔坐标</param>
        /// <returns>球坐标 (x=经度, y=纬度)</returns>
        public static Vector2 FromCartesian(Vector3 cartesian)
        {
            float radius = cartesian.magnitude;
            float longitude = Mathf.Atan2(cartesian.x, cartesian.z);
            float latitude = Mathf.Asin(cartesian.y / radius);
            return new Vector2(longitude, latitude);
        }

        /// <summary>
        /// 球坐标转笛卡尔坐标
        /// </summary>
        /// <param name="spherical">球坐标 (x=经度, y=纬度)</param>
        /// <returns>笛卡尔坐标</returns>
        public static Vector3 ToCartesian(Vector2 spherical)
        {
            float longitude = spherical.x;
            float latitude = spherical.y;

            float x = Mathf.Cos(latitude) * Mathf.Sin(longitude);
            float y = Mathf.Sin(latitude);
            float z = Mathf.Cos(latitude) * Mathf.Cos(longitude);

            return new Vector3(x, y, z);
        }

        /// <summary>
        /// 绘制球面上的纬度线
        /// </summary>
        public static void DrawLatitudeLine(Vector3 sphereCenter, float radius, float latitude, Color color, int segments)
        {
            Gizmos.color = color;
            DrawGreatCircle(sphereCenter, radius, latitude, true, segments);
        }

        /// <summary>
        /// 绘制球面上的经度线
        /// </summary>
        public static void DrawLongitudeLine(Vector3 sphereCenter, float radius, float longitude, Color color, int segments)
        {
            Gizmos.color = color;
            DrawGreatCircle(sphereCenter, radius, longitude, false, segments);
        }

        /// <summary>
        /// 绘制大圆（纬线或经线）
        /// </summary>
        private static void DrawGreatCircle(Vector3 center, float radius, float angle, bool isLatitude, int segments)
        {
            Vector3 prevPoint = Vector3.zero;
            bool firstPoint = true;

            float startAngle = isLatitude ? -Mathf.PI : -Mathf.PI / 2f;
            float endAngle = isLatitude ? Mathf.PI : Mathf.PI / 2f;

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float currentAngle = Mathf.Lerp(startAngle, endAngle, t);

                Vector2 spherical = isLatitude
                    ? new Vector2(currentAngle, angle)
                    : new Vector2(angle, currentAngle);

                Vector3 localPos = ToCartesian(spherical) * radius;
                Vector3 worldPos = center + localPos;

                if (!firstPoint)
                {
                    Gizmos.DrawLine(prevPoint, worldPos);
                }

                prevPoint = worldPos;
                firstPoint = false;
            }
        }

        /// <summary>
        /// 绘制完整的球面网格
        /// </summary>
        public static void DrawSphereGrid(Vector3 center, float radius, int latitudeLines, int longitudeLines, Color color)
        {
            Gizmos.color = color;

            // 绘制纬度线
            for (int i = 0; i <= latitudeLines; i++)
            {
                float lat = Mathf.Lerp(-Mathf.PI / 2f, Mathf.PI / 2f, (float)i / latitudeLines);
                DrawLatitudeLine(center, radius, lat, color, longitudeLines * 4);
            }

            // 绘制经度线
            for (int i = 0; i <= longitudeLines; i++)
            {
                float lon = Mathf.Lerp(-Mathf.PI, Mathf.PI, (float)i / longitudeLines);
                DrawLongitudeLine(center, radius, lon, color, latitudeLines * 4);
            }
        }
    }
}
