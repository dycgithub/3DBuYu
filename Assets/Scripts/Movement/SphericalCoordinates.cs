using UnityEngine;

namespace SphereMovement
{
    /// <summary>
    /// 球面坐标工具类
    /// 提供笛卡尔坐标和球面坐标之间的转换方法
    /// </summary>
    public static class SphericalCoordinates
    {
        /// <summary>
        /// 将笛卡尔坐标转换为球面坐标
        /// </summary>
        /// <param name="cartesian">单位球面上的笛卡尔坐标 (x, y, z)</param>
        /// <returns>球面坐标 (longitude, latitude)，单位为弧度</returns>
        /// <remarks>
        /// longitude (x): 经度，范围 [-π, π]，从Z轴正方向顺时针测量
        /// latitude (y): 纬度，范围 [-π/2, π/2]，从赤道向极点测量
        /// </remarks>
        public static Vector2 FromCartesian(Vector3 cartesian)
        {
            // 处理零向量
            float magnitude = cartesian.magnitude;
            if (magnitude < 0.0001f)
            {
                return Vector2.zero;
            }

            // 归一化
            Vector3 normalized = cartesian / magnitude;

            // 经度：使用 atan2(x, z)，因为我们在XZ平面上测量角度
            float longitude = Mathf.Atan2(normalized.x, normalized.z);

            // 纬度：使用 asin(y)，Y轴是极轴
            float latitude = Mathf.Asin(normalized.y);

            return new Vector2(longitude, latitude);
        }

        /// <summary>
        /// 将球面坐标转换为笛卡尔坐标（单位球面）
        /// </summary>
        /// <param name="spherical">球面坐标 (longitude, latitude)，单位为弧度</param>
        /// <returns>单位球面上的笛卡尔坐标 (x, y, z)</returns>
        /// <remarks>
        /// longitude (x): 经度，0 时指向 Z 轴正方向
        /// latitude (y): 纬度，π/2 时指向 Y 轴正方向（北极），-π/2 时指向 Y 轴负方向（南极）
        /// </remarks>
        public static Vector3 ToCartesian(Vector2 spherical)
        {
            float longitude = spherical.x;
            float latitude = spherical.y;

            // 使用球面坐标公式
            // x = cos(latitude) * sin(longitude)
            // y = sin(latitude)
            // z = cos(latitude) * cos(longitude)
            float cosLatitude = Mathf.Cos(latitude);

            float x = cosLatitude * Mathf.Sin(longitude);
            float y = Mathf.Sin(latitude);
            float z = cosLatitude * Mathf.Cos(longitude);

            return new Vector3(x, y, z);
        }
    }
}