using SphereMovement.Interfaces;
using UnityEngine;

namespace SphereMovement.Input
{
    /// <summary>
    /// 键盘输入提供器
    /// 使用键盘按键作为输入源
    /// </summary>
    public class KeyboardInputProvider : IInputProvider
    {
        /// <summary>
        /// 左移按键
        /// </summary>
        public KeyCode LeftKey { get; set; } = KeyCode.A;

        /// <summary>
        /// 右移按键
        /// </summary>
        public KeyCode RightKey { get; set; } = KeyCode.D;

        /// <summary>
        /// 上移按键
        /// </summary>
        public KeyCode UpKey { get; set; } = KeyCode.W;

        /// <summary>
        /// 下移按键
        /// </summary>
        public KeyCode DownKey { get; set; } = KeyCode.S;

        /// <inheritdoc/>
        public float Horizontal
        {
            get
            {
                float value = 0f;
                if (UnityEngine.Input.GetKey(LeftKey)) value -= 1f;
                if (UnityEngine.Input.GetKey(RightKey)) value += 1f;
                return value;
            }
        }

        /// <inheritdoc/>
        public float Vertical
        {
            get
            {
                float value = 0f;
                if (UnityEngine.Input.GetKey(DownKey)) value -= 1f;
                if (UnityEngine.Input.GetKey(UpKey)) value += 1f;
                return value;
            }
        }

        /// <inheritdoc/>
        public bool HasInput => Horizontal != 0f || Vertical != 0f;
    }
}
