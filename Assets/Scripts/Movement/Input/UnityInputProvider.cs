using SphereMovement.Interfaces;
using UnityEngine;

namespace SphereMovement.Input
{
    /// <summary>
    /// Unity 输入提供器
    /// 包装 Unity 的 Input 系统
    /// </summary>
    public class UnityInputProvider : IInputProvider
    {
        /// <summary>
        /// 水平轴输入名称
        /// </summary>
        public string HorizontalAxisName { get; set; } = "Horizontal";

        /// <summary>
        /// 垂直轴输入名称
        /// </summary>
        public string VerticalAxisName { get; set; } = "Vertical";

        /// <summary>
        /// 输入死区阈值
        /// </summary>
        public float DeadZone { get; set; } = 0.001f;

        /// <inheritdoc/>
        public float Horizontal => UnityEngine.Input.GetAxis(HorizontalAxisName);

        /// <inheritdoc/>
        public float Vertical => UnityEngine.Input.GetAxis(VerticalAxisName);

        /// <inheritdoc/>
        public bool HasInput =>
            Mathf.Abs(Horizontal) > DeadZone ||
            Mathf.Abs(Vertical) > DeadZone;
    }
}
