using UnityEngine;

namespace SphereMovement.Interfaces
{
    /// <summary>
    /// 移动策略接口
    /// 定义不同移动模式的通用契约
    /// </summary>
    public interface IMovementStrategy
    {
        /// <summary>
        /// 执行移动
        /// </summary>
        /// <param name="target">目标变换</param>
        /// <param name="input">输入向量 (x=水平, y=垂直)</param>
        /// <param name="deltaTime">时间增量</param>
        void Move(Transform target, Vector2 input, float deltaTime);

        /// <summary>
        /// 初始化策略
        /// </summary>
        /// <param name="worldPosition">初始世界位置</param>
        void InitializeFromPosition(Vector3 worldPosition);

        /// <summary>
        /// 获取当前速度
        /// </summary>
        Vector3 GetCurrentVelocity();

        /// <summary>
        /// 是否可以在当前位置停止
        /// </summary>
        bool CanStopAtCurrentPosition();
    }
}
