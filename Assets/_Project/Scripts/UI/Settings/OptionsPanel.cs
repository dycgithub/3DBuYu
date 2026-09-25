using System.Collections.Generic;
using _Project.UI.Animations;
using GameSystem;
using Services;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

namespace _Project.UI.Settings
{
    /// <summary>
    /// 设置面板：处理音频、窗口显示、帧率和现有输入动作重绑定。
    /// </summary>
    public class OptionsPanel : MonoBehaviour
    {
        private static readonly int[] FrameRateOptions = { -1, 30, 60, 120, 144 };

        [Header("面板")]
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Button _openButton;
        [SerializeField] private Button _returnBaseButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private UIPanelShowHide _panelTween;

        [Header("音频")]
        [SerializeField] private Slider _masterVolumeSlider;
        [SerializeField] private Slider _bgmVolumeSlider;
        [SerializeField] private Slider _sfxVolumeSlider;

        [Header("显示")]
        [SerializeField] private Toggle _fullscreenToggle;
        [SerializeField] private Toggle _vSyncToggle;
        [SerializeField] private Toggle _combatRangeVisualsToggle;
        [SerializeField] private Button _frameRateButton;

        [Inject] private OptionsService optionsService;
        private IInputService _inputService;

        private CanvasGroup _panelGroup;
        private readonly Dictionary<(InputBindingAction action, InputBindingSlot slot), Button> _bindingButtons = new();
        private readonly Dictionary<(InputBindingAction action, InputBindingSlot slot), UnityAction> _bindingListeners = new();
        private bool _isRebinding;
        private (InputBindingAction action, InputBindingSlot slot) _activeBinding;

        private void Awake()
        {
            _panelRoot ??= gameObject;
            _panelGroup = _panelRoot.GetComponent<CanvasGroup>();
            _openButton ??= _panelRoot.transform.parent?.Find("OptionButton")?.GetComponent<Button>();
            _closeButton ??= FindComponent<Button>("CloseButton");
            _masterVolumeSlider ??= FindComponent<Slider>("MasterVolume");
            _bgmVolumeSlider ??= FindComponent<Slider>("BgmVolume");
            _sfxVolumeSlider ??= FindComponent<Slider>("SfxVolume");
            _fullscreenToggle ??= FindComponent<Toggle>("Fullscreen");
            _vSyncToggle ??= FindComponent<Toggle>("VSync");
            _combatRangeVisualsToggle ??= FindComponent<Toggle>("CombatRangeVisuals");
            _frameRateButton ??= FindComponent<Button>("FrameRate");
        }

        private void Start()
        {
            _openButton?.onClick.AddListener(Toggle);
            _closeButton?.onClick.AddListener(Hide);

            if (_returnBaseButton != null)
            {
                _returnBaseButton.onClick.AddListener(ReturnToBase);
                bool inBattle = SceneManager.GetSceneByName("GameScene").isLoaded;
                _returnBaseButton.gameObject.SetActive(inBattle);
            }

            BindSettingsControls();
            BindInputControls();
        }

        [Inject]
        private void ResolveInput(IObjectResolver resolver)
        {
            resolver.TryResolve(out _inputService);
        }

        /// <summary>切换设置面板的显示状态。</summary>
        public void Toggle()
        {
            if (_panelTween != null)
            {
                _panelTween.Toggle();
                return;
            }

            CanvasGroup group = GetPanelGroup();
            if (group == null)
            {
                return;
            }

            bool visible = group.alpha > 0.01f;
            SetPanelVisible(!visible);
        }

        private void BindSettingsControls()
        {
            if (optionsService == null)
            {
                return;
            }

            optionsService.Apply();
            SettingsData settings = optionsService.Current;

            if (_masterVolumeSlider != null)
            {
                _masterVolumeSlider.SetValueWithoutNotify(settings.masterVolume);
                _masterVolumeSlider.onValueChanged.AddListener(optionsService.SetMasterVolume);
            }

            if (_bgmVolumeSlider != null)
            {
                _bgmVolumeSlider.SetValueWithoutNotify(settings.bgmVolume);
                _bgmVolumeSlider.onValueChanged.AddListener(optionsService.SetBgmVolume);
            }

            if (_sfxVolumeSlider != null)
            {
                _sfxVolumeSlider.SetValueWithoutNotify(settings.sfxVolume);
                _sfxVolumeSlider.onValueChanged.AddListener(optionsService.SetSfxVolume);
            }

            if (_fullscreenToggle != null)
            {
                _fullscreenToggle.SetIsOnWithoutNotify(settings.fullscreen);
                _fullscreenToggle.onValueChanged.AddListener(HandleFullscreenChanged);
            }

            if (_vSyncToggle != null)
            {
                _vSyncToggle.SetIsOnWithoutNotify(settings.vSync);
                _vSyncToggle.onValueChanged.AddListener(optionsService.SetVSync);
            }

            if (_combatRangeVisualsToggle != null)
            {
                _combatRangeVisualsToggle.SetIsOnWithoutNotify(settings.showCombatRangeVisuals);
                _combatRangeVisualsToggle.onValueChanged.AddListener(
                    optionsService.SetShowCombatRangeVisuals);
            }

            if (_frameRateButton != null)
            {
                _frameRateButton.onClick.AddListener(AdvanceFrameRate);
                RefreshFrameRateLabel();
            }
        }

        private void BindInputControls()
        {
            if (_inputService == null)
            {
                return;
            }

            IReadOnlyList<InputBindingAction> actions = _inputService.BindingActions;
            for (int i = 0; i < actions.Count; i++)
            {
                InputBindingAction action = actions[i];
                BindInputControl(action, InputBindingSlot.Primary);
                if (IsMoveAction(action))
                {
                    BindInputControl(action, InputBindingSlot.Secondary);
                }
            }
        }

        private void BindInputControl(InputBindingAction action, InputBindingSlot slot)
        {
            Button button = FindComponent<Button>($"Binding_{action}_{slot}");
            if (button == null)
            {
                return;
            }

            UnityAction listener = () => BeginRebind(action, slot);
            (InputBindingAction action, InputBindingSlot slot) key = (action, slot);
            _bindingButtons[key] = button;
            _bindingListeners[key] = listener;
            button.onClick.AddListener(listener);
            RefreshBindingLabel(action, slot);
        }

        private void HandleFullscreenChanged(bool enabled)
        {
            optionsService?.SetWindowMode(enabled ? WindowMode.Fullscreen : WindowMode.Windowed);
        }

        private void AdvanceFrameRate()
        {
            if (optionsService == null)
            {
                return;
            }

            SettingsData settings = optionsService.Current;
            int currentIndex = GetFrameRateIndex(settings);
            int nextValue = FrameRateOptions[(currentIndex + 1) % FrameRateOptions.Length];
            optionsService.SetFrameRateLimit(nextValue >= 0, nextValue >= 0 ? nextValue : 60);
            RefreshFrameRateLabel();
        }

        private void RefreshFrameRateLabel()
        {
            if (_frameRateButton == null || optionsService == null)
            {
                return;
            }

            SettingsData settings = optionsService.Current;
            string text = settings.frameRateLimitEnabled
                ? $"帧率上限：{settings.frameRateLimit} FPS"
                : "帧率上限：不限制";
            TMP_Text label = _frameRateButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = text;
            }
        }

        private static int GetFrameRateIndex(SettingsData settings)
        {
            if (!settings.frameRateLimitEnabled)
            {
                return 0;
            }

            for (int i = 1; i < FrameRateOptions.Length; i++)
            {
                if (FrameRateOptions[i] == settings.frameRateLimit)
                {
                    return i;
                }
            }

            return 2;
        }

        private void BeginRebind(InputBindingAction action, InputBindingSlot slot)
        {
            (InputBindingAction action, InputBindingSlot slot) key = (action, slot);
            if (_inputService == null || !_bindingButtons.TryGetValue(key, out Button button))
            {
                return;
            }

            _activeBinding = key;
            _isRebinding = true;
            SetBindingLabel(button, "请按下按键...");
            _inputService.Rebind(
                action,
                slot,
                () => _isRebinding = true,
                () => FinishRebind(action, slot),
                () => FinishRebind(action, slot));
        }

        private void FinishRebind(InputBindingAction action, InputBindingSlot slot)
        {
            RefreshBindingLabel(action, slot);
            if (IsActiveBinding(action, slot))
            {
                _isRebinding = false;
            }
        }

        private void RefreshBindingLabel(InputBindingAction action, InputBindingSlot slot)
        {
            (InputBindingAction action, InputBindingSlot slot) key = (action, slot);
            if (!_bindingButtons.TryGetValue(key, out Button button) || _inputService == null)
            {
                return;
            }

            SetBindingLabel(button, _inputService.GetBindingDisplayString(action, slot));
        }

        private static bool IsMoveAction(InputBindingAction action)
        {
            return action == InputBindingAction.MoveUp
                || action == InputBindingAction.MoveDown
                || action == InputBindingAction.MoveLeft
                || action == InputBindingAction.MoveRight;
        }

        private static void SetBindingLabel(Button button, string text)
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.text = text;
            }
        }

        private void Hide()
        {
            CancelActiveRebind();
            if (_panelTween != null)
            {
                _panelTween.Hide();
                return;
            }

            SetPanelVisible(false);
        }

        private void ReturnToBase()
        {
            if (_sceneLoader != null)
            {
                Hide();
                _sceneLoader.ReturnToMainMenu();
            }
        }

        [Inject] private SceneLoader _sceneLoader;

        private void SetPanelVisible(bool visible)
        {
            CanvasGroup group = GetPanelGroup();
            if (group == null)
            {
                return;
            }

            group.alpha = visible ? 1f : 0f;
            group.blocksRaycasts = visible;
            group.interactable = visible;
        }

        private CanvasGroup GetPanelGroup()
        {
            if (_panelGroup == null && _panelRoot != null)
            {
                _panelGroup = _panelRoot.GetComponent<CanvasGroup>();
            }

            return _panelGroup;
        }

        private T FindComponent<T>(string objectName) where T : Component
        {
            Transform root = _panelRoot != null ? _panelRoot.transform : transform;
            T[] components = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i].name == objectName)
                {
                    return components[i];
                }
            }

            return null;
        }

        private void OnDisable()
        {
            CancelActiveRebind();
        }

        private void OnDestroy()
        {
            CancelActiveRebind();
            _openButton?.onClick.RemoveListener(Toggle);
            _closeButton?.onClick.RemoveListener(Hide);
            _returnBaseButton?.onClick.RemoveListener(ReturnToBase);
            if (optionsService != null)
            {
                _masterVolumeSlider?.onValueChanged.RemoveListener(optionsService.SetMasterVolume);
                _bgmVolumeSlider?.onValueChanged.RemoveListener(optionsService.SetBgmVolume);
                _sfxVolumeSlider?.onValueChanged.RemoveListener(optionsService.SetSfxVolume);
                _vSyncToggle?.onValueChanged.RemoveListener(optionsService.SetVSync);
                _combatRangeVisualsToggle?.onValueChanged.RemoveListener(
                    optionsService.SetShowCombatRangeVisuals);
            }

            _fullscreenToggle?.onValueChanged.RemoveListener(HandleFullscreenChanged);
            _frameRateButton?.onClick.RemoveListener(AdvanceFrameRate);

            foreach (KeyValuePair<(InputBindingAction action, InputBindingSlot slot), Button> pair in _bindingButtons)
            {
                pair.Value.onClick.RemoveListener(_bindingListeners[pair.Key]);
            }
        }

        private void CancelActiveRebind()
        {
            bool wasRebinding = _isRebinding;
            (InputBindingAction action, InputBindingSlot slot) binding = _activeBinding;
            _inputService?.CancelRebind();
            _isRebinding = false;
            if (wasRebinding)
            {
                RefreshBindingLabel(binding.action, binding.slot);
            }
        }

        private bool IsActiveBinding(InputBindingAction action, InputBindingSlot slot)
        {
            return _activeBinding.action == action && _activeBinding.slot == slot;
        }
    }
}
