using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using GameSystem;
using Services;
using SaveSystem = GameSystem.SaveSystem;

namespace InputSystem
{
    public class InputService : MonoBehaviour, IInputService
    {
        private GameInput _gameInput;
        private PlayerInputActionOverrides _overrides;
        private InputAction[] _portFireActions;
        private InputActionRebindingExtensions.RebindingOperation _rebind;
        private bool _suppressCancelCallback;

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
        };

        public Vector2 Move => _gameInput != null ? _gameInput.Player.Move.ReadValue<Vector2>() : Vector2.zero;
        public bool PausePressedThisFrame => _gameInput != null && _gameInput.Player.Pause.WasPressedThisFrame();
        public event Action PausePressed;

        private void Awake()
        {
            _gameInput = new GameInput();
            _overrides = new PlayerInputActionOverrides();
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

            _gameInput.Player.Enable();
            _gameInput.Player.Pause.performed += OnPausePerformed;
            LoadOverrides();
        }

        private void OnDisable()
        {
            CancelRebind();
            if (_gameInput == null)
            {
                return;
            }

            _gameInput.Player.Pause.performed -= OnPausePerformed;
            _gameInput.Player.Disable();
        }

        private void OnDestroy()
        {
            CancelRebind();
            _gameInput?.Dispose();
            _gameInput = null;
        }

        private void OnPausePerformed(InputAction.CallbackContext ctx)
        {
            PausePressed?.Invoke();
        }

        public bool IsPortFireHeld(int portIndex)
        {
            if (_gameInput == null)
            {
                return false;
            }

            return GetPortFire(portIndex).IsPressed();
        }

        public bool IsKeyHeld(KeyCode key)
        {
            return key != KeyCode.None && UnityEngine.Input.GetKey(key);
        }

        private InputAction GetPortFire(int portIndex)
        {
            if (portIndex < 0 || portIndex >= _portFireActions.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(portIndex));
            }

            return _portFireActions[portIndex];
        }

        public void RebindPause(Action onStarted, Action onCompleted, Action onCanceled)
        {
            Rebind(
                InputBindingAction.Pause,
                InputBindingSlot.Primary,
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

            (InputAction pauseAction, int bindingIndex) = ResolveBinding(
                InputBindingAction.Pause,
                InputBindingSlot.Primary);
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
            for (int i = 0; i < _bindingActions.Length; i++)
            {
                InputBindingAction action = _bindingActions[i];
                ApplyOverride(action, InputBindingSlot.Primary);
                if (SupportsSecondary(action))
                {
                    ApplyOverride(action, InputBindingSlot.Secondary);
                }
            }
        }

        /// <summary>
        /// 获取指定动作当前生效的显示名称。
        /// </summary>
        /// <param name="action">要查询的输入动作。</param>
        /// <param name="slot">要查询的主绑定或备用绑定。</param>
        /// <returns>输入系统生成的按键显示文本。</returns>
        public string GetBindingDisplayString(
            InputBindingAction action,
            InputBindingSlot slot)
        {
            if (_gameInput == null)
            {
                return string.Empty;
            }

            (InputAction inputAction, int bindingIndex) = ResolveBinding(action, slot);
            return inputAction.GetBindingDisplayString(bindingIndex);
        }

        /// <summary>
        /// 启动指定动作的交互式按键重绑定。
        /// </summary>
        /// <param name="action">要重绑定的输入动作。</param>
        /// <param name="slot">要重绑定的主绑定或备用绑定。</param>
        /// <param name="onStarted">开始监听按键时回调。</param>
        /// <param name="onCompleted">重绑定成功时回调。</param>
        /// <param name="onCanceled">重绑定取消时回调。</param>
        public void Rebind(
            InputBindingAction action,
            InputBindingSlot slot,
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
            (InputAction inputAction, int bindingIndex) = ResolveBinding(action, slot);
            inputAction.Disable();

            _rebind = inputAction.PerformInteractiveRebinding(bindingIndex)
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(operation =>
                {
                    operation.Dispose();
                    _rebind = null;
                    _suppressCancelCallback = false;
                    inputAction.Enable();
                    SaveOverride(action, slot, inputAction.bindings[bindingIndex].overridePath);
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

        private (InputAction action, int bindingIndex) ResolveBinding(
            InputBindingAction bindingAction,
            InputBindingSlot bindingSlot)
        {
            if (bindingSlot == InputBindingSlot.Secondary && !SupportsSecondary(bindingAction))
            {
                throw new ArgumentOutOfRangeException(nameof(bindingSlot), bindingSlot, null);
            }

            InputAction inputAction = GetInputAction(bindingAction);
            if (!SupportsSecondary(bindingAction))
            {
                return (inputAction, 0);
            }

            string partName = GetMovePartName(bindingAction);
            int targetOccurrence = bindingSlot == InputBindingSlot.Primary ? 0 : 1;
            int occurrence = 0;
            for (int index = 0; index < inputAction.bindings.Count; index++)
            {
                InputBinding binding = inputAction.bindings[index];
                if (!binding.isPartOfComposite || binding.name != partName)
                {
                    continue;
                }

                if (occurrence == targetOccurrence)
                {
                    return (inputAction, index);
                }

                occurrence++;
            }

            throw new InvalidOperationException(
                $"Missing {bindingAction} {bindingSlot} binding in the Move action.");
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
                _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
            };
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

        private static bool SupportsSecondary(InputBindingAction action)
        {
            return action == InputBindingAction.MoveUp
                || action == InputBindingAction.MoveDown
                || action == InputBindingAction.MoveLeft
                || action == InputBindingAction.MoveRight;
        }

        private void ApplyOverride(InputBindingAction action, InputBindingSlot slot)
        {
            string overridePath = GetBindingOverride(action, slot);
            if (string.IsNullOrEmpty(overridePath))
            {
                return;
            }

            try
            {
                (InputAction inputAction, int bindingIndex) = ResolveBinding(action, slot);
                inputAction.ApplyBindingOverride(bindingIndex, overridePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning(
                    $"[InputService] Failed to apply {action} {slot} binding override: {e.Message}");
            }
        }

        private string GetBindingOverride(InputBindingAction action, InputBindingSlot slot)
        {
            int actionIndex = (int)action;
            List<string> paths = slot == InputBindingSlot.Secondary
                ? _overrides?.secondaryBindingOverridePaths
                : _overrides?.bindingOverridePaths;
            if (paths != null
                && actionIndex < paths.Count
                && !string.IsNullOrEmpty(paths[actionIndex]))
            {
                return paths[actionIndex];
            }

            return action == InputBindingAction.Pause && slot == InputBindingSlot.Primary
                ? _overrides?.pauseBindingOverridePath
                : null;
        }

        private string GetOverride(InputBindingAction action)
        {
            return GetBindingOverride(action, InputBindingSlot.Primary);
        }

        private void SaveOverride(
            InputBindingAction action,
            InputBindingSlot slot,
            string overridePath)
        {
            _overrides ??= new PlayerInputActionOverrides();
            List<string> paths = slot == InputBindingSlot.Secondary
                ? _overrides.secondaryBindingOverridePaths ??= new List<string>()
                : _overrides.bindingOverridePaths ??= new List<string>();
            int actionIndex = (int)action;
            while (paths.Count <= actionIndex)
            {
                paths.Add(null);
            }

            paths[actionIndex] = overridePath;
            if (action == InputBindingAction.Pause && slot == InputBindingSlot.Primary)
            {
                _overrides.pauseBindingOverridePath = overridePath;
            }

            SaveSystem.SaveInputOverrides(_overrides);
        }
    }
}
