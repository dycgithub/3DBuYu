using System.Collections.Generic;
using _Project.UI.Animations;
using CombatSystem;
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
    /// <summary>设置面板：管理语言、音频、显示、攻击模式、场景返回和输入重绑定。</summary>
    public partial class OptionsPanel : MonoBehaviour
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
        [SerializeField] private Button _resetBindingsButton;

        [Header("攻击模式")]
        [SerializeField] private ToggleGroup _attackModeToggleGroup;
        [SerializeField] private Toggle _attackModeAutomaticToggle;
        [SerializeField] private Toggle _attackModeKeyToggle;
        [SerializeField] private Toggle _attackModeMouseToggle;

        [Inject] private OptionsService optionsService;
        private IInputService _inputService;

        private CanvasGroup _panelGroup;
        private readonly Dictionary<InputBindingAction, Button> _bindingButtons = new();
        private readonly Dictionary<InputBindingAction, UnityAction> _bindingListeners = new();
        private bool _isRebinding;
        private InputBindingAction _activeBinding;

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
            _resetBindingsButton ??= FindComponent<Button>("ResetBindings");
            _attackModeToggleGroup ??= FindComponent<ToggleGroup>("AttackModeOptions");
            _attackModeAutomaticToggle ??= FindComponent<Toggle>("AttackModeAutomatic");
            _attackModeKeyToggle ??= FindComponent<Toggle>("AttackModeKey");
            _attackModeMouseToggle ??= FindComponent<Toggle>("AttackModeMouse");
            _attackModeMarkedTargetToggle ??= FindComponent<Toggle>("AttackModeMarkedTarget");
        }

        private void Start()
        {
            _openButton?.onClick.AddListener(Toggle);
            _closeButton?.onClick.AddListener(Hide);

            if (_returnBaseButton != null)
            {
                _returnBaseButton.onClick.AddListener(ReturnToBase);
                bool inBattle = _sceneLoader != null &&
                    SceneManager.GetSceneByName(_sceneLoader.gameScene).isLoaded;
                _returnBaseButton.gameObject.SetActive(inBattle);
            }

            BindSettingsControls();
            BindLanguage();
            BindInputControls();
            _resetBindingsButton?.onClick.AddListener(ResetBindings);
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

            BindAttackModeControls(settings.attackMode);
        }

        /// <summary>按存档初始化互斥攻击模式 Toggle。</summary>
        private void BindAttackModeControls(TransmitterAttackMode selectedMode)
        {
            if (_attackModeToggleGroup != null)
            {
                _attackModeToggleGroup.allowSwitchOff = false;
            }

            BindAttackModeToggle(
                _attackModeAutomaticToggle, selectedMode,
                TransmitterAttackMode.Automatic, OnAutoAttackChanged);
            BindAttackModeToggle(
                _attackModeKeyToggle, selectedMode,
                TransmitterAttackMode.Key, OnKeyAttackChanged);
            BindAttackModeToggle(
                _attackModeMouseToggle, selectedMode,
                TransmitterAttackMode.Mouse, OnMouseAttackChanged);
            BindAttackModeToggle(
                _attackModeMarkedTargetToggle, selectedMode,
                TransmitterAttackMode.MarkedTarget, OnMarkedTargetAttackChanged);
        }

        /// <summary>设置单个攻击模式 Toggle 并绑定选择事件。</summary>
        private static void BindAttackModeToggle(
            Toggle toggle,
            TransmitterAttackMode selectedMode,
            TransmitterAttackMode mode,
            UnityAction<bool> listener)
        {
            if (toggle == null)
            {
                return;
            }

            toggle.SetIsOnWithoutNotify(selectedMode == mode);
            toggle.onValueChanged.AddListener(listener);
        }

        private void OnAutoAttackChanged(bool selected)
        {
            if (selected)
            {
                optionsService?.SetAttackMode(TransmitterAttackMode.Automatic);
            }
        }

        private void OnKeyAttackChanged(bool selected)
        {
            if (selected)
            {
                optionsService?.SetAttackMode(TransmitterAttackMode.Key);
            }
        }

        private void OnMouseAttackChanged(bool selected)
        {
            if (selected)
            {
                optionsService?.SetAttackMode(TransmitterAttackMode.Mouse);
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
                BindInputControl(actions[i]);
            }
        }

        private void BindInputControl(InputBindingAction action)
        {
            Button button = FindComponent<Button>($"Binding_{action}_Primary");
            if (button == null)
            {
                return;
            }

            UnityAction listener = () => BeginRebind(action);
            _bindingButtons[action] = button;
            _bindingListeners[action] = listener;
            button.onClick.AddListener(listener);
            RefreshBindingLabel(action);
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
                ? L10n.Get("settings.frameRate", settings.frameRateLimit)
                : L10n.Get("settings.uncapped");
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

        private void BeginRebind(InputBindingAction action)
        {
            if (_inputService == null || !_bindingButtons.TryGetValue(action, out Button button))
            {
                return;
            }

            _activeBinding = action;
            _isRebinding = true;
            SetBindingLabel(button, L10n.Get("settings.pressKey"));
            _inputService.Rebind(
                action,
                () => _isRebinding = true,
                () => FinishRebind(action),
                () => FinishRebind(action));
        }

        private void FinishRebind(InputBindingAction action)
        {
            RefreshBindingLabel(action);
            if (IsActiveBinding(action))
            {
                _isRebinding = false;
            }
        }

        private void RefreshBindingLabel(InputBindingAction action)
        {
            if (!_bindingButtons.TryGetValue(action, out Button button) || _inputService == null)
            {
                return;
            }

            SetBindingLabel(button, _inputService.GetBindingDisplayString(action));
        }

        /// <summary>恢复所有可重绑动作的默认键位并刷新显示。</summary>
        private void ResetBindings()
        {
            if (_inputService == null)
            {
                return;
            }

            CancelActiveRebind();
            _inputService.ResetBindings();

            IReadOnlyList<InputBindingAction> actions = _inputService.BindingActions;
            for (int i = 0; i < actions.Count; i++)
            {
                RefreshBindingLabel(actions[i]);
            }
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
            UnbindLanguage();
            CancelActiveRebind();
            _openButton?.onClick.RemoveListener(Toggle);
            _closeButton?.onClick.RemoveListener(Hide);
            _returnBaseButton?.onClick.RemoveListener(ReturnToBase);
            _resetBindingsButton?.onClick.RemoveListener(ResetBindings);
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
            _attackModeAutomaticToggle?.onValueChanged.RemoveListener(OnAutoAttackChanged);
            _attackModeKeyToggle?.onValueChanged.RemoveListener(OnKeyAttackChanged);
            _attackModeMouseToggle?.onValueChanged.RemoveListener(OnMouseAttackChanged);
            _attackModeMarkedTargetToggle?.onValueChanged.RemoveListener(OnMarkedTargetAttackChanged);

            foreach (KeyValuePair<InputBindingAction, Button> pair in _bindingButtons)
            {
                pair.Value.onClick.RemoveListener(_bindingListeners[pair.Key]);
            }
        }

        private void CancelActiveRebind()
        {
            bool wasRebinding = _isRebinding;
            InputBindingAction binding = _activeBinding;
            _inputService?.CancelRebind();
            _isRebinding = false;
            if (wasRebinding)
            {
                RefreshBindingLabel(binding);
            }
        }

        private bool IsActiveBinding(InputBindingAction action)
        {
            return _activeBinding == action;
        }
    }
}
