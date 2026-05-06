using UnityEngine;

namespace SphereMovement.Interfaces
{
    /// <summary>
    /// 朝向控制器接口
    /// </summary>
    public interface IOrientationController
    {
        /// <summary>
        /// 球心位置
        /// </summary>
        Vector3 SphereCenter { get; set; }

        /// <summary>
        /// 更新物体的朝向
        /// </summary>
        /// <param name="target">目标变换</param>
        /// <param name="normalizedPos">归一化位置（单位球面）</param>
        /// <param name="currentCoords">当前球坐标</param>
        void UpdateOrientation(Transform target, Vector3 normalizedPos, Vector2 currentCoords);

        /// <summary>
        /// 获取纬线切线方向（东西方向）
        /// </summary>
        Vector3 GetLatitudeTangent(Vector3 normalizedPos);

        /// <summary>
        /// 获取经线切线方向（南北方向）
        /// </summary>
        Vector3 GetLongitudeTangent(Vector3 normalizedPos);
    }
}
