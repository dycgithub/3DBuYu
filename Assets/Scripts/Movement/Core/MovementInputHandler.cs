using SphereMovement.Interfaces;
using UnityEngine;

namespace SphereMovement.Core
{
    /// <summary>
    /// 移动输入处理器
    /// </summary>
    public class MovementInputHandler : IMovementInputHandler
    {
        private readonly IInputProvider _inputProvider;

        /// <summary>
        /// 构造函数
        /// </summary>
        public MovementInputHandler(IInputProvider inputProvider)
        {
            _inputProvider = inputProvider ?? throw new System.ArgumentNullException(nameof(inputProvider));
        }

        /// <inheritdoc/>
        public float MoveSpeed { get; set; } = 30f;

        /// <inheritdoc/>
        public bool HasActiveInput => _inputProvider.HasInput;

        /// <inheritdoc/>
        public Vector2 ProcessInput(float deltaTime)
        {
            if (!_inputProvider.HasInput)
            {
                return Vector2.zero;
            }

            float horizontal = _inputProvider.Horizontal;
            float vertical = _inputProvider.Vertical;

            // 将角度转换为弧度
            float longitudeDelta = horizontal * MoveSpeed * deltaTime * Mathf.Deg2Rad;
            float latitudeDelta = vertical * MoveSpeed * deltaTime * Mathf.Deg2Rad;

            return new Vector2(longitudeDelta, latitudeDelta);
        }

        /// <summary>
        /// 限制纬度范围
        /// </summary>
        /// <param name="latitude">纬度值（弧度）</param>
        /// <returns>限制后的纬度</returns>
        public static float ClampLatitude(float latitude)
        {
            float maxLat = Mathf.PI / 2f - 0.001f;
            return Mathf.Clamp(latitude, -maxLat, maxLat);
        }
    }
}
