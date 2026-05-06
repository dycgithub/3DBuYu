using SphereMovement.Interfaces;

namespace SphereMovement.Input
{
    /// <summary>
    /// 模拟输入提供器
    /// 用于测试或AI控制
    /// </summary>
    public class MockInputProvider : IInputProvider
    {
        private float _horizontal;
        private float _vertical;
        private float _deadZone = 0.001f;

        /// <summary>
        /// 设置水平输入
        /// </summary>
        public void SetHorizontal(float value)
        {
            _horizontal = value;
        }

        /// <summary>
        /// 设置垂直输入
        /// </summary>
        public void SetVertical(float value)
        {
            _vertical = value;
        }

        /// <summary>
        /// 设置输入死区
        /// </summary>
        public void SetDeadZone(float value)
        {
            _deadZone = value;
        }

        /// <summary>
        /// 清除所有输入
        /// </summary>
        public void Clear()
        {
            _horizontal = 0f;
            _vertical = 0f;
        }

        /// <inheritdoc/>
        public float Horizontal => _horizontal;

        /// <inheritdoc/>
        public float Vertical => _vertical;

        /// <inheritdoc/>
        public bool HasInput =>
            UnityEngine.Mathf.Abs(_horizontal) > _deadZone ||
            UnityEngine.Mathf.Abs(_vertical) > _deadZone;
    }
}
