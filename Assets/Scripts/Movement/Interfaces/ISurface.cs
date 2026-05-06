using UnityEngine;

namespace SphereMovement.Interfaces
{
    /// <summary>
    /// 表面接口
    /// 定义移动表面的通用契约
    /// </summary>
    public interface ISurface
    {
        /// <summary>
        /// 获取表面上某一点的法线方向
        /// </summary>
        /// <param name="position">世界坐标位置</param>
        /// <returns>法线方向</returns>
        Vector3 GetNormalAtPosition(Vector3 position);

        /// <summary>
        /// 获取表面上某一点的切线方向（可选方向）
        /// </summary>
        /// <param name="position">世界坐标位置</param>
        /// <param name="directionHint">方向提示</param>
        /// <returns>切线方向</returns>
        Vector3 GetTangentAtPosition(Vector3 position, Vector3 directionHint);

        /// <summary>
        /// 将位置限制在表面上
        /// </summary>
        /// <param name="position">原始位置</param>
        /// <returns>限制后的位置</returns>
        Vector3 ClampPositionToSurface(Vector3 position);

        /// <summary>
        /// 获取表面上最近的点
        /// </summary>
        /// <param name="worldPosition">世界位置</param>
        /// <returns>表面上最近的点</returns>
        Vector3 GetClosestPointOnSurface(Vector3 worldPosition);

        /// <summary>
        /// 检查点是否在表面上（允许误差）
        /// </summary>
        /// <param name="position">位置</param>
        /// <param name="tolerance">容差</param>
        /// <returns>是否在表面上</returns>
        bool IsPointOnSurface(Vector3 position, float tolerance = 0.01f);
    }
}
