using SphereMovement.Interfaces;
using UnityEngine;

namespace SphereMovement.Input
{
    /// <summary>
    /// 移动输入组件
    /// 挂载此组件以提供移动输入
    /// </summary>
    [AddComponentMenu("Movement/Movement Input")]
    public class MovementInput : MonoBehaviour, IInputProvider
    {
        [Header("输入设置")]
        [Tooltip("输入模式")]
        [SerializeField] private InputMode inputMode = InputMode.UnityAxis;

        [Tooltip("水平轴输入名称（仅UnityAxis模式）")]
        [SerializeField] private string horizontalAxisName = "Horizontal";

        [Tooltip("垂直轴输入名称（仅UnityAxis模式）")]
        [SerializeField] private string verticalAxisName = "Vertical";

        [Tooltip("正交输入键（仅Keyboard模式）")]
        [SerializeField] private KeyCode upKey = KeyCode.W;

        [Tooltip("负交输入键（仅Keyboard模式）")]
        [SerializeField] private KeyCode downKey = KeyCode.S;

        [Tooltip("正横输入键（仅Keyboard模式）")]
        [SerializeField] private KeyCode rightKey = KeyCode.D;

        [Tooltip("负横输入键（仅Keyboard模式）")]
        [SerializeField] private KeyCode leftKey = KeyCode.A;

        [Tooltip("输入死区阈值")]
        [SerializeField] private float deadZone = 0.001f;

        // 当前输入值
        private float _horizontal;
        private float _vertical;
        private bool _hasInput;

        /// <summary>
        /// 水平输入 (-1 到 1)
        /// </summary>
        public float Horizontal => _horizontal;

        /// <summary>
        /// 垂直输入 (-1 到 1)
        /// </summary>
        public float Vertical => _vertical;

        /// <summary>
        /// 是否有有效输入
        /// </summary>
        public bool HasInput => _hasInput;

        /// <summary>
        /// 当前输入模式
        /// </summary>
        public InputMode Mode => inputMode;

        private void Update()
        {
            ReadInput();
        }

        /// <summary>
        /// 读取输入
        /// </summary>
        private void ReadInput()
        {
            switch (inputMode)
            {
                case InputMode.UnityAxis:
                    ReadUnityAxisInput();
                    break;
                case InputMode.Keyboard:
                    ReadKeyboardInput();
                    break;
                case InputMode.Mock:
                    // Mock模式下由外部设置值
                    break;
            }

            _hasInput = Mathf.Abs(_horizontal) > deadZone || Mathf.Abs(_vertical) > deadZone;
        }

        /// <summary>
        /// 读取 Unity 轴输入
        /// </summary>
        private void ReadUnityAxisInput()
        {
            _horizontal = UnityEngine.Input.GetAxis(horizontalAxisName);
            _vertical = UnityEngine.Input.GetAxis(verticalAxisName);
        }

        /// <summary>
        /// 读取键盘输入
        /// </summary>
        private void ReadKeyboardInput()
        {
            _horizontal = 0f;
            _vertical = 0f;

            if (UnityEngine.Input.GetKey(rightKey)) _horizontal += 1f;
            if (UnityEngine.Input.GetKey(leftKey)) _horizontal -= 1f;
            if (UnityEngine.Input.GetKey(upKey)) _vertical += 1f;
            if (UnityEngine.Input.GetKey(downKey)) _vertical -= 1f;
        }

        /// <summary>
        /// 设置输入值（用于 Mock 模式或程序化控制）
        /// </summary>
        public void SetInput(float horizontal, float vertical)
        {
            _horizontal = Mathf.Clamp(horizontal, -1f, 1f);
            _vertical = Mathf.Clamp(vertical, -1f, 1f);
            _hasInput = Mathf.Abs(_horizontal) > deadZone || Mathf.Abs(_vertical) > deadZone;
        }

        /// <summary>
        /// 清除输入
        /// </summary>
        public void ClearInput()
        {
            _horizontal = 0f;
            _vertical = 0f;
            _hasInput = false;
        }

        /// <summary>
        /// 切换输入模式
        /// </summary>
        public void SetInputMode(InputMode mode)
        {
            inputMode = mode;
        }
    }

    /// <summary>
    /// 输入模式枚举
    /// </summary>
    public enum InputMode
    {
        /// <summary>Unity 轴输入</summary>
        UnityAxis,

        /// <summary>键盘按键输入</summary>
        Keyboard,

        /// <summary>模拟输入（用于测试或程序化控制）</summary>
        Mock
    }
}
