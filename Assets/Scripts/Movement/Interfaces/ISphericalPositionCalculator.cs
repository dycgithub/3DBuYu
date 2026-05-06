using UnityEngine;

namespace SphereMovement.Interfaces
{
    /// <summary>
    /// 球面位置计算器接口
    /// </summary>
    public interface ISphericalPositionCalculator
    {
        /// <summary>
        /// 球心位置
        /// </summary>
        Vector3 SphereCenter { get; set; }

        /// <summary>
        /// 球半径
        /// </summary>
        float SphereRadius { get; set; }

        /// <summary>
        /// 计算球面上的位置
        /// </summary>
        Vector3 CalculatePosition(Vector2 sphericalCoords);

        /// <summary>
        /// 计算归一化位置（单位球面）
        /// </summary>
        Vector3 CalculateNormalizedPosition(Vector2 sphericalCoords);

        /// <summary>
        /// 从笛卡尔坐标转换为球坐标
        /// </summary>
        Vector2 CartesianToSpherical(Vector3 cartesian);
    }
}
