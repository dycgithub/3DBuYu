using UnityEngine;

/// <summary>
/// 球坐标工具类：直角坐标与球坐标互转，以及球面切向量基底计算。
/// </summary>
public static class SphericalCoordinates
{
    /// <summary>
    /// 直角坐标转球坐标。
    /// </summary>
    /// <param name="position">目标点的世界坐标</param>
    /// <param name="center">球心世界坐标</param>
    /// <param name="longitude">经度（XZ 平面内从 +Z 轴起算），范围 [-PI, PI]</param>
    /// <param name="latitude">纬度（从 XZ 平面向 +Y 方向），范围 [-PI/2, PI/2]</param>
    /// <param name="radius">球心到该点的距离</param>
    public static void FromCartesian(Vector3 position, Vector3 center,
        out float longitude, out float latitude, out float radius)
    {
        Vector3 rel = position - center;
        radius = rel.magnitude;
        if (radius < 1e-6f)
        {
            longitude = 0f;
            latitude = 0f;
            return;
        }

        longitude = Mathf.Atan2(rel.x, rel.z);
        latitude = Mathf.Asin(rel.y / radius);
    }

    /// <summary>
    /// 球坐标转直角坐标。
    /// </summary>
    public static Vector3 ToCartesian(float longitude, float latitude, float radius, Vector3 center)
    {
        float cosLat = Mathf.Cos(latitude);
        float x = radius * cosLat * Mathf.Sin(longitude);
        float y = radius * Mathf.Sin(latitude);
        float z = radius * cosLat * Mathf.Cos(longitude);
        return center + new Vector3(x, y, z);
    }

    /// <summary>
    /// 获取球面上某点的切向量基底。
    /// east：经度增加方向（纬度线方向）。
    /// north：纬度增加方向（经度线方向）。
    /// </summary>
    /// <param name="normal">该点的球面法线（球心指向表面）</param>
    public static void GetTangentBasis(Vector3 normal, out Vector3 east, out Vector3 north)
    {
        east = Vector3.Cross(Vector3.up, normal);
        if (east.sqrMagnitude < 1e-6f)
            east = Vector3.right;
        east.Normalize();
        north = Vector3.Cross(normal, east);
        north.Normalize();
    }
}
