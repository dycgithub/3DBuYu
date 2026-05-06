using SphereMovement.Interfaces;
using UnityEngine;

namespace SphereMovement.Input
{
    /// <summary>
    /// 表面移动输入处理器
    /// 将原始输入转换为表面移动的参数
    /// </summary>
    public class SurfaceMovementInput : IMovementInputHandler
    {
        private readonly IInputProvider _inputProvider;

        /// <summary>
        /// 移动速度（度/秒）
        /// </summary>
        public float MoveSpeed { get; set; } = 30f;

        /// <summary>
        /// 是否有活动输入
        /// </summary>
        public bool HasActiveInput => _inputProvider?.HasInput ?? false;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="inputProvider">输入提供器</param>
        public SurfaceMovementInput(IInputProvider inputProvider)
        {
            _inputProvider = inputProvider ?? throw new System.ArgumentNullException(nameof(inputProvider));
        }

        /// <summary>
        /// 处理输入并返回表面移动参数
        /// </summary>
        /// <param name="deltaTime">时间增量</param>
        /// <returns>移动参数 (x=横向变化, y=纵向变化)</returns>
        public Vector2 ProcessInput(float deltaTime)
        {
            if (!_inputProvider.HasInput)
            {
                return Vector2.zero;
            }

            float horizontal = _inputProvider.Horizontal;
            float vertical = _inputProvider.Vertical;

            // 将角度转换为弧度
            float horizontalDelta = horizontal * MoveSpeed * deltaTime * Mathf.Deg2Rad;
            float verticalDelta = vertical * MoveSpeed * deltaTime * Mathf.Deg2Rad;

            return new Vector2(horizontalDelta, verticalDelta);
        }
    }
}
