using UnityEngine;

/// <summary>
/// 球面切向量工具；固定参考基底用于静态布局和镜头初始朝向。
/// </summary>
public static class SphericalCoordinates
{
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
        {
            east = Vector3.right;
        }
        east.Normalize();
        north = Vector3.Cross(normal, east);
        north.Normalize();
    }
}
