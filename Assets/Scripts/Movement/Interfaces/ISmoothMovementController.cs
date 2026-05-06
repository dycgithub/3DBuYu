using UnityEngine;

namespace SphereMovement.Interfaces
{
    /// <summary>
    /// 平滑移动控制器接口
    /// </summary>
    public interface ISmoothMovementController
    {
        /// <summary>
        /// 是否启用平滑移动
        /// </summary>
        bool UseSmoothMovement { get; set; }

        /// <summary>
        /// 平滑时间
        /// </summary>
        float SmoothTime { get; set; }

        /// <summary>
        /// 平滑移动到目标位置
        /// </summary>
        /// <param name="current">当前球坐标</param>
        /// <param name="target">目标球坐标</param>
        /// <returns>更新后的球坐标</returns>
        Vector2 SmoothMove(Vector2 current, Vector2 target);

        /// <summary>
        /// 重置平滑状态
        /// </summary>
        void Reset();
    }
}
