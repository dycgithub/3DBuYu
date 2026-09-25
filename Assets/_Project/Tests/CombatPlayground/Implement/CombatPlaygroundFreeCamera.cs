using UnityEngine;
using UnityEngine.InputSystem;

namespace _Project.Tests.CombatPlayground.Implement
{
    /// <summary>
    /// 提供仅作用于 CombatPlayground 场景的 WASD 自由漫游相机。
    /// </summary>
    public sealed class CombatPlaygroundFreeCamera : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float moveSpeed = 8f;
        [SerializeField, Min(0.1f)] private float fastMultiplier = 2.5f;
        [SerializeField, Min(0.01f)] private float lookSpeed = 0.12f;

        /// <summary>每帧处理键盘移动和右键视角旋转。</summary>
        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                Move(keyboard);
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                transform.Rotate(Vector3.up, delta.x * lookSpeed, Space.World);
                transform.Rotate(Vector3.left, delta.y * lookSpeed, Space.Self);
            }
        }

        /// <summary>按 WASD、QE 和 Shift 移动相机。</summary>
        private void Move(Keyboard keyboard)
        {
            Vector3 direction = Vector3.zero;
            if (keyboard.wKey.isPressed)
            {
                direction += Vector3.forward;
            }
            if (keyboard.sKey.isPressed)
            {
                direction += Vector3.back;
            }
            if (keyboard.aKey.isPressed)
            {
                direction += Vector3.left;
            }
            if (keyboard.dKey.isPressed)
            {
                direction += Vector3.right;
            }
            if (keyboard.eKey.isPressed)
            {
                direction += Vector3.up;
            }
            if (keyboard.qKey.isPressed)
            {
                direction += Vector3.down;
            }

            if (direction.sqrMagnitude <= 0f)
            {
                return;
            }

            float multiplier = keyboard.leftShiftKey.isPressed ? fastMultiplier : 1f;
            transform.Translate(
                direction.normalized * moveSpeed * multiplier * Time.unscaledDeltaTime,
                Space.Self);
        }
    }
}
