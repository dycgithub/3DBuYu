using System;
using System.Collections.Generic;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;
using GameSystem;
using Services;
using SaveSystem = GameSystem.SaveSystem;

namespace InputSystem
{
    /// <summary>从 Input System Action 读取玩家输入并管理攻击输入门控。</summary>
    public class InputService : MonoBehaviour, IInputService
    {
        private const string KeyboardControlPath = "<Keyboard>/*";

        private GameInput _gameInput;
        private PlayerInputActionOverrides _overrides;
        private InputAction[] _portFireActions;
        private InputAction _mouseFireAction;
        private InputAction _pointerPositionAction;
        private readonly ReactiveProperty<bool> _mouseFireHeld = new(false);
        private InputActionRebindingExtensions.RebindingOperation _rebind;
        private bool _suppressCancelCallback;
        private AttackInputState _attackInputState = AttackInputState.Allowed;

        private static readonly InputBindingAction[] _bindingActions =
        {
            InputBindingAction.MoveUp,
            InputBindingAction.MoveDown,
            InputBindingAction.MoveLeft,
            InputBindingAction.MoveRight,
            InputBindingAction.Pause,
            InputBindingAction.PortFire1,
            InputBindingAction.PortFire2,
            InputBindingAction.PortFire3,
            InputBindingAction.PortFire4,
            InputBindingAction.PortFire5,
            InputBindingAction.PortFire6,
            InputBindingAction.PortFire7,
            InputBindingAction.PortFire8,
            InputBindingAction.CameraZoomIn,
            InputBindingAction.CameraZoomOut,
            InputBindingAction.CameraOrbitLeft,
            InputBindingAction.CameraOrbitRight,
            InputBindingAction.CameraOrbitUp,
            InputBindingAction.CameraOrbitDown,
        };

        public Vector2 Move => _gameInput != null ? _gameInput.Player.Move.ReadValue<Vector2>() : Vector2.zero;
        public float CameraDistanceInput => _gameInput == null
            ? 0f
            : ReadButton(_gameInput.Player.CameraZoomOut) - ReadButton(_gameInput.Player.CameraZoomIn);
        public Vector2 CameraOrbitInput => _gameInput == null
            ? Vector2.zero
            : new Vector2(
                ReadButton(_gameInput.Player.CameraOrbitRight) - ReadButton(_gameInput.Player.CameraOrbitLeft),
                ReadButton(_gameInput.Player.CameraOrbitUp) - ReadButton(_gameInput.Player.CameraOrbitDown));
        public bool PausePressedThisFrame => _gameInput != null && _gameInput.Player.Pause.WasPressedThisFrame();
        public ReadOnlyReactiveProperty<bool> MouseFireHeld => _mouseFireHeld;
        public Vector2 PointerPosition => _pointerPositionAction != null
            ? _pointerPositionAction.ReadValue<Vector2>()
            : Vector2.zero;
        public AttackInputState CurrentAttackInputState => _attackInputState;
        public event Action PausePressed;
        public event Action MouseFirePressed;

        private void Awake()
        {
            _gameInput = new GameInput();
            _overrides = new PlayerInputActionOverrides();
            _mouseFireAction = _gameInput.Player.MouseFire;
            _pointerPositionAction = _gameInput.Player.PointerPosition;
            _portFireActions = new InputAction[]
            {
                _gameInput.Player.PortFire1, _gameInput.Player.PortFire2,
                _gameInput.Player.PortFire3, _gameInput.Player.PortFire4,
                _gameInput.Player.PortFire5, _gameInput.Player.PortFire6,
                _gameInput.Player.PortFire7, _gameInput.Player.PortFire8,
            };
        }

        public int MaxPorts => _portFireActions?.Length ?? 0;
        public IReadOnlyList<InputBindingAction> BindingActions => _bindingActions;

        private void OnEnable()
        {
            if (_gameInput == null)
            {
                return;
            }

            _mouseFireAction.started += OnMouseFireStarted;
            _mouseFireAction.canceled += OnMouseFireCanceled;
            _gameInput.Player.Pause.performed += OnPausePerformed;
            _gameInput.Player.Enable();
            _mouseFireHeld.Value = _attackInputState == AttackInputState.Allowed &&
                _mouseFireAction.IsPressed();
            LoadOverrides();
        }

        private void OnDisable()
        {
            CancelRebind();
            _mouseFireHeld.Value = false;
            if (_gameInput == null)
            {
                return;
            }

            _mouseFireAction.started -= OnMouseFireStarted;
            _mouseFireAction.canceled -= OnMouseFireCanceled;
            _gameInput.Player.Pause.performed -= OnPausePerformed;
            _gameInput.Player.Disable();
        }

        private void OnDestroy()
        {
            CancelRebind();
            _mouseFireHeld.Dispose();
            _gameInput?.Dispose();
            _gameInput = null;
        }

        private void OnPausePerformed(InputAction.CallbackContext ctx)
        {
            PausePressed?.Invoke();
        }

        /// <summary>仅在攻击门控允许时发布鼠标开火按下状态。</summary>
        private void OnMouseFireStarted(InputAction.CallbackContext context)
        {
            bool allowed = _attackInputState == AttackInputState.Allowed;
            _mouseFireHeld.Value = allowed;
            if (allowed)
            {
                MouseFirePressed?.Invoke();
            }
        }

        /// <summary>鼠标开火动作释放时清除持续按住状态。</summary>
        private void OnMouseFireCanceled(InputAction.CallbackContext context)
        {
            _mouseFireHeld.Value = false;
        }

        public bool IsPortFireHeld(int portIndex)
        {
            if (_gameInput == null)
            {
                return false;
            }

            return GetPortFire(portIndex).IsPressed();
        }

        /// <summary>切换攻击门控；屏蔽期间丢弃鼠标按住状态，恢复后需重新按下。</summary>
        /// <param name="state">攻击输入允许或屏蔽状态。</param>
        public void SetAttackInputState(AttackInputState state)
        {
            _attackInputState = state;
            if (state == AttackInputState.Blocked)
            {
                _mouseFireHeld.Value = false;
            }
        }

        public bool IsMenuActionHeld(MenuInputAction action)
        {
            return _gameInput != null && GetMenuAction(action).IsPressed();
        }

        /// <summary>移除所有可重绑动作的覆盖，并保存默认键位配置。</summary>
        public void ResetBindings()
        {
            CancelRebind();
            if (_gameInput != null)
            {
                for (int index = 0; index < _bindingActions.Length; index++)
                {
                    (InputAction action, int bindingIndex) = ResolveBinding(_bindingActions[index]);
                    action.RemoveBindingOverride(bindingIndex);
                }
            }

            _overrides = new PlayerInputActionOverrides();
            SaveSystem.SaveInputOverrides(_overrides);
        }

        private InputAction GetPortFire(int portIndex)
        {
            if (portIndex < 0 || portIndex >= _portFireActions.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(portIndex));
            }

            return _portFireActions[portIndex];
        }

        private InputAction GetMenuAction(MenuInputAction action)
        {
            return action switch
            {
                MenuInputAction.Settings => _gameInput.Player.MenuSettings,
                MenuInputAction.Supply => _gameInput.Player.MenuSupply,
                MenuInputAction.Backpack => _gameInput.Player.MenuBackpack,
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
            };
        }

        public void RebindPause(Action onStarted, Action onCompleted, Action onCanceled)
        {
            Rebind(
                InputBindingAction.Pause,
                onStarted,
                onCompleted,
                onCanceled);
        }

        public string GetPauseBindingPath()
        {
            if (_gameInput == null)
            {
                return "<Keyboard>/escape";
            }

            string savedPath = GetOverride(InputBindingAction.Pause);
            if (!string.IsNullOrEmpty(savedPath))
            {
                return savedPath;
            }

            (InputAction pauseAction, int bindingIndex) = ResolveBinding(InputBindingAction.Pause);
            if (pauseAction.bindings.Count > bindingIndex)
            {
                string overridePath = pauseAction.bindings[bindingIndex].overridePath;
                if (!string.IsNullOrEmpty(overridePath))
                {
                    return overridePath;
                }

                return pauseAction.bindings[bindingIndex].effectivePath;
            }
            return "<Keyboard>/escape";
        }

        private void LoadOverrides()
        {
            _overrides = SaveSystem.LoadInputOverrides();
            bool changed = RemoveInvalidKeyboardOverrides();
            changed |= RemoveLegacyOrbitOverrides();
            if (changed)
            {
                SaveSystem.SaveInputOverrides(_overrides);
            }

            for (int i = 0; i < _bindingActions.Length; i++)
            {
                InputBindingAction action = _bindingActions[i];
                ApplyOverride(action);
            }
        }

        /// <summary>
        /// 获取指定动作当前生效的显示名称。
        /// </summary>
        /// <param name="action">要查询的输入动作。</param>
        /// <returns>输入系统生成的按键显示文本。</returns>
        public string GetBindingDisplayString(
            InputBindingAction action)
        {
            if (_gameInput == null)
            {
                return string.Empty;
            }

            (InputAction inputAction, int bindingIndex) = ResolveBinding(action);
            return inputAction.GetBindingDisplayString(bindingIndex);
        }

        /// <summary>
        /// 启动指定动作的交互式按键重绑定。
        /// </summary>
        /// <param name="action">要重绑定的输入动作。</param>
        /// <param name="onStarted">开始监听按键时回调。</param>
        /// <param name="onCompleted">重绑定成功时回调。</param>
        /// <param name="onCanceled">重绑定取消时回调。</param>
        public void Rebind(
            InputBindingAction action,
            Action onStarted,
            Action onCompleted,
            Action onCanceled)
        {
            if (_gameInput == null)
            {
                onCanceled?.Invoke();
                return;
            }

            CancelCurrentRebind();
            (InputAction inputAction, int bindingIndex) = ResolveBinding(action);
            inputAction.Disable();

            InputActionRebindingExtensions.RebindingOperation rebind =
                inputAction.PerformInteractiveRebinding(bindingIndex)
                    .OnMatchWaitForAnother(0.1f);
            if (IsKeyboardAction(action))
            {
                rebind
                    .WithControlsHavingToMatchPath(KeyboardControlPath)
                    .OnPotentialMatch(operation =>
                    {
                        if (!IsKeyboardControl(operation.selectedControl))
                        {
                            operation.Cancel();
                        }
                    });
            }

            _rebind = rebind
                .OnComplete(operation =>
                {
                    operation.Dispose();
                    _rebind = null;
                    _suppressCancelCallback = false;
                    inputAction.Enable();
                    SaveOverride(action, inputAction.bindings[bindingIndex].overridePath);
                    onCompleted?.Invoke();
                })
                .OnCancel(operation =>
                {
                    operation.Dispose();
                    _rebind = null;
                    inputAction.Enable();
                    bool notify = !_suppressCancelCallback;
                    _suppressCancelCallback = false;
                    if (notify)
                    {
                        onCanceled?.Invoke();
                    }
                });

            onStarted?.Invoke();
            _rebind.Start();
        }

        /// <summary>
        /// 取消当前重绑定，并恢复被临时禁用的输入动作。
        /// </summary>
        public void CancelRebind()
        {
            if (_rebind == null)
            {
                return;
            }

            _suppressCancelCallback = true;
            _rebind.Cancel();
        }

        private void CancelCurrentRebind()
        {
            if (_rebind == null)
            {
                return;
            }

            _rebind.Cancel();
        }

        private (InputAction action, int bindingIndex) ResolveBinding(InputBindingAction bindingAction)
        {
            InputAction inputAction = GetInputAction(bindingAction);
            if (!IsMoveAction(bindingAction))
            {
                return (inputAction, 0);
            }

            string partName = GetMovePartName(bindingAction);
            for (int index = 0; index < inputAction.bindings.Count; index++)
            {
                InputBinding binding = inputAction.bindings[index];
                if (binding.isPartOfComposite && binding.name == partName)
                {
                    return (inputAction, index);
                }
            }

            throw new InvalidOperationException(
                $"Missing {bindingAction} binding in the Move action.");
        }

        private InputAction GetInputAction(InputBindingAction action)
        {
            return action switch
            {
                InputBindingAction.MoveUp => _gameInput.Player.Move,
                InputBindingAction.MoveDown => _gameInput.Player.Move,
                InputBindingAction.MoveLeft => _gameInput.Player.Move,
                InputBindingAction.MoveRight => _gameInput.Player.Move,
                InputBindingAction.Pause => _gameInput.Player.Pause,
                InputBindingAction.PortFire1 => _gameInput.Player.PortFire1,
                InputBindingAction.PortFire2 => _gameInput.Player.PortFire2,
                InputBindingAction.PortFire3 => _gameInput.Player.PortFire3,
                InputBindingAction.PortFire4 => _gameInput.Player.PortFire4,
                InputBindingAction.PortFire5 => _gameInput.Player.PortFire5,
                InputBindingAction.PortFire6 => _gameInput.Player.PortFire6,
                InputBindingAction.PortFire7 => _gameInput.Player.PortFire7,
                InputBindingAction.PortFire8 => _gameInput.Player.PortFire8,
                InputBindingAction.CameraZoomIn => _gameInput.Player.CameraZoomIn,
                InputBindingAction.CameraZoomOut => _gameInput.Player.CameraZoomOut,
                InputBindingAction.CameraOrbitLeft => _gameInput.Player.CameraOrbitLeft,
                InputBindingAction.CameraOrbitRight => _gameInput.Player.CameraOrbitRight,
                InputBindingAction.CameraOrbitUp => _gameInput.Player.CameraOrbitUp,
                InputBindingAction.CameraOrbitDown => _gameInput.Player.CameraOrbitDown,
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
            };
        }

        /// <summary>将指定按钮的按下状态转成零或一。</summary>
        /// <param name="action">要读取的输入动作。</param>
        /// <returns>按下时为一，否则为零。</returns>
        private static float ReadButton(InputAction action)
        {
            return action.IsPressed() ? 1f : 0f;
        }

        private static string GetMovePartName(InputBindingAction action)
        {
            return action switch
            {
                InputBindingAction.MoveUp => "up",
                InputBindingAction.MoveDown => "down",
                InputBindingAction.MoveLeft => "left",
                InputBindingAction.MoveRight => "right",
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
            };
        }

        private static bool IsMoveAction(InputBindingAction action)
        {
            return action == InputBindingAction.MoveUp
                || action == InputBindingAction.MoveDown
                || action == InputBindingAction.MoveLeft
                || action == InputBindingAction.MoveRight;
        }

        /// <summary>判断输入动作是否属于相机控制。</summary>
        /// <param name="action">要检查的绑定动作。</param>
        /// <returns>相机控制动作返回 true。</returns>
        private static bool IsCameraAction(InputBindingAction action)
        {
            return action == InputBindingAction.CameraZoomIn
                || action == InputBindingAction.CameraZoomOut
                || action == InputBindingAction.CameraOrbitLeft
                || action == InputBindingAction.CameraOrbitRight
                || action == InputBindingAction.CameraOrbitUp
                || action == InputBindingAction.CameraOrbitDown;
        }

        /// <summary>判断输入动作是否限制为键盘绑定。</summary>
        /// <param name="action">要检查的绑定动作。</param>
        /// <returns>移动和相机控制动作返回 true。</returns>
        private static bool IsKeyboardAction(InputBindingAction action)
        {
            return IsMoveAction(action) || IsCameraAction(action);
        }

        /// <summary>清除移动和相机动作中不符合键盘限制的旧绑定。</summary>
        /// <returns>存档内容是否发生变化。</returns>
        private bool RemoveInvalidKeyboardOverrides()
        {
            bool changed = false;
            for (int i = 0; i < _bindingActions.Length; i++)
            {
                InputBindingAction action = _bindingActions[i];
                if (!IsKeyboardAction(action))
                {
                    continue;
                }

                changed |= RemoveInvalidOverride(_overrides.bindingOverridePaths, action);
            }

            return changed;
        }

        /// <summary>移除旧版把 Q/E 留给相机环绕的覆盖，避免与缩放默认键冲突。</summary>
        /// <returns>存档中是否存在需要迁移的旧环绕绑定。</returns>
        private bool RemoveLegacyOrbitOverrides()
        {
            bool changed = RemoveLegacyOverride(
                InputBindingAction.CameraOrbitLeft,
                "<Keyboard>/q");
            changed |= RemoveLegacyOverride(
                InputBindingAction.CameraOrbitRight,
                "<Keyboard>/e");
            return changed;
        }

        private bool RemoveLegacyOverride(InputBindingAction action, string legacyPath)
        {
            List<string> paths = _overrides?.bindingOverridePaths;
            int index = (int)action;
            if (paths == null || index >= paths.Count ||
                !string.Equals(paths[index], legacyPath, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            paths[index] = null;
            return true;
        }

        private static bool RemoveInvalidOverride(
            List<string> paths,
            InputBindingAction action)
        {
            int index = (int)action;
            if (paths == null || index >= paths.Count)
            {
                return false;
            }

            string path = paths[index];
            if (string.IsNullOrEmpty(path) || IsKeyboardBindingPath(path))
            {
                return false;
            }

            paths[index] = null;
            return true;
        }

        private static bool IsKeyboardBindingPath(string path)
        {
            return !string.IsNullOrEmpty(path)
                && path.StartsWith("<Keyboard>/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsKeyboardControl(InputControl control)
        {
            return control != null && InputControlPath.Matches(KeyboardControlPath, control);
        }

        private void ApplyOverride(InputBindingAction action)
        {
            string overridePath = GetBindingOverride(action);
            if (string.IsNullOrEmpty(overridePath)
                || (IsKeyboardAction(action) && !IsKeyboardBindingPath(overridePath)))
            {
                return;
            }

            try
            {
                (InputAction inputAction, int bindingIndex) = ResolveBinding(action);
                inputAction.ApplyBindingOverride(bindingIndex, overridePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    $"[InputService] Failed to apply {action} binding override: {e.Message}");
            }
        }

        private string GetBindingOverride(InputBindingAction action)
        {
            int actionIndex = (int)action;
            List<string> paths = _overrides?.bindingOverridePaths;
            if (paths != null
                && actionIndex < paths.Count
                && !string.IsNullOrEmpty(paths[actionIndex]))
            {
                return paths[actionIndex];
            }

            return action == InputBindingAction.Pause
                ? _overrides?.pauseBindingOverridePath
                : null;
        }

        private string GetOverride(InputBindingAction action)
        {
            return GetBindingOverride(action);
        }

        private void SaveOverride(
            InputBindingAction action,
            string overridePath)
        {
            _overrides ??= new PlayerInputActionOverrides();
            List<string> paths = _overrides.bindingOverridePaths ??= new List<string>();
            int actionIndex = (int)action;
            while (paths.Count <= actionIndex)
            {
                paths.Add(null);
            }

            paths[actionIndex] = overridePath;
            if (action == InputBindingAction.Pause)
            {
                _overrides.pauseBindingOverridePath = overridePath;
            }

            SaveSystem.SaveInputOverrides(_overrides);
        }
    }
}
