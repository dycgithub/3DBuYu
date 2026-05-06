using SphereMovement.Data;
using UnityEngine;

namespace SphereMovement.Core
{
    /// <summary>
    /// 平面移动策略
    /// 在标准平面上移动物体
    /// </summary>
    public class PlaneMovementStrategy : MovementStrategyBase
    {
        private Vector3 _currentVelocity;
        private Vector3 _targetPosition;

        public PlaneMovementStrategy(MovementConfig config) : base(config)
        {
        }

        public override void Move(Transform target, Vector2 input, float deltaTime)
        {
            if (input.sqrMagnitude < Config.InputDeadZone)
            {
                // 没有输入时减速
                _currentVelocity = Vector3.Lerp(_currentVelocity, Vector3.zero, deltaTime / Config.VelocitySmoothTime);
                return;
            }

            // 计算移动方向
            Vector3 moveDir = new Vector3(input.x, 0f, input.y).normalized;

            // 计算目标速度
            Vector3 targetVelocity = moveDir * Config.MoveSpeed;

            // 平滑速度变化
            _currentVelocity = Vector3.Lerp(_currentVelocity, targetVelocity, deltaTime / Config.VelocitySmoothTime);

            // 计算目标位置
            Vector3 targetPos = target.position + _currentVelocity * deltaTime;

            // 应用位置
            if (Config.PositionSmoothTime > 0.001f)
            {
                target.position = Vector3.SmoothDamp(
                    target.position,
                    targetPos,
                    ref _currentVelocity,
                    Config.PositionSmoothTime
                );
            }
            else
            {
                target.position = targetPos;
            }

            // 更新目标朝向（如果需要）
            if (Config.RotationSpeed > 0f)
            {
                Quaternion targetRot = Quaternion.LookRotation(moveDir, Vector3.up);
                target.rotation = Quaternion.Slerp(
                    target.rotation,
                    targetRot,
                    Config.RotationSpeed * deltaTime
                );
            }
        }

        /// <summary>
        /// 从世界位置初始化策略（平面移动直接使用位置）
        /// </summary>
        public override void InitializeFromPosition(Vector3 worldPosition)
        {
            // 平面移动直接设置目标位置
            _targetPosition = worldPosition;
            // 可以选择重置速度
            _currentVelocity = Vector3.zero;
        }

        /// <summary>
        /// 获取当前速度
        /// </summary>
        public override Vector3 GetCurrentVelocity()
        {
            return _currentVelocity;
        }

        /// <summary>
        /// 是否可以在当前位置停止（平面移动总是可以停止）
        /// </summary>
        public override bool CanStopAtCurrentPosition()
        {
            return true;
        }
    }
}
