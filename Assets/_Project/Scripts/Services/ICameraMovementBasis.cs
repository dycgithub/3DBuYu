using UnityEngine;

namespace Services
{
    /// <summary>
    /// 提供与渲染摄像机解耦的球面移动方向。
    /// 逻辑基底由摄像机编排器维护，避免镜头惯性或锁定构图改变玩家输入语义。
    /// </summary>
    public interface ICameraMovementBasis
    {
        /// <summary>
        /// 将屏幕输入转换为当前球面切线方向。
        /// </summary>
        /// <param name="input">屏幕左右、上下输入，长度会被限制在一以内。</param>
        /// <param name="surfaceNormal">玩家所在球面的单位法线。</param>
        /// <returns>贴合球面切平面的世界方向，输入为零时返回零向量。</returns>
        Vector3 GetMoveDirection(Vector2 input, Vector3 surfaceNormal);
    }
}
