using System;
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

        private void OnEnable()
        {
            if (_gameInput == null) return;
            _gameInput.Player.Enable();
            _gameInput.Player.Pause.performed += OnPausePerformed;
            LoadOverrides();
        }

        private void OnDisable()
        {
            if (_gameInput == null) return;
            _gameInput.Player.Pause.performed -= OnPausePerformed;
            _gameInput.Player.Disable();
        }

        private void OnDestroy()
        {
            _gameInput?.Dispose();
            _gameInput = null;
        }

        private void OnPausePerformed(InputAction.CallbackContext ctx)
        {
            PausePressed?.Invoke();
        }

        public bool IsPortFireHeld(int portIndex)
        {
            if (_gameInput == null) return false;
            return GetPortFire(portIndex).IsPressed();
        }

        private InputAction GetPortFire(int portIndex)
        {
            if (portIndex < 0 || portIndex >= _portFireActions.Length)
                throw new ArgumentOutOfRangeException(nameof(portIndex));
            return _portFireActions[portIndex];
        }

        public void RebindPause(Action onStarted, Action onCompleted, Action onCanceled)
        {
            var pauseAction = _gameInput.Player.Pause;
            pauseAction.Disable();

            var rebind = pauseAction.PerformInteractiveRebinding(0);
            rebind
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(_ =>
                {
                    rebind.Dispose();
                    pauseAction.Enable();
                    SaveOverride(pauseAction.bindings[0].overridePath);
                    onCompleted?.Invoke();
                })
                .OnCancel(_ =>
                {
                    rebind.Dispose();
                    pauseAction.Enable();
                    onCanceled?.Invoke();
                })
                .Start();

            onStarted?.Invoke();
        }

        public string GetPauseBindingPath()
        {
            if (_gameInput == null) return "<Keyboard>/escape";
            var pauseAction = _gameInput.Player.Pause;
            if (pauseAction.bindings.Count > 0)
            {
                string overridePath = pauseAction.bindings[0].overridePath;
                if (!string.IsNullOrEmpty(overridePath))
                    return overridePath;
                return pauseAction.bindings[0].effectivePath;
            }
            return "<Keyboard>/escape";
        }

        private void LoadOverrides()
        {
            _overrides = SaveSystem.LoadInputOverrides();
            if (_overrides != null && !string.IsNullOrEmpty(_overrides.pauseBindingOverridePath))
            {
                try
                {
                    _gameInput.Player.Pause.ApplyBindingOverride(0, _overrides.pauseBindingOverridePath);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[InputService] Failed to apply pause binding override: {e.Message}");
                }
            }
        }

        private void SaveOverride(string overridePath)
        {
            _overrides ??= new PlayerInputActionOverrides();
            _overrides.pauseBindingOverridePath = overridePath;
            SaveSystem.SaveInputOverrides(_overrides);
        }
    }
}
