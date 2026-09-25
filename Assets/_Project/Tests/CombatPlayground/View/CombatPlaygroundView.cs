using UnityEngine;
using UnityEngine.UIElements;
using VContainer;
using _Project.Tests.CombatPlayground.Implement;
using _Project.Tests.CombatPlayground.Model;

namespace _Project.Tests.CombatPlayground.View
{
    /// <summary>
    /// 绑定 CombatPlayground 的静态 UI Toolkit 控件，不创建运行时 UI。
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class CombatPlaygroundView : MonoBehaviour
    {
        private UIDocument _document;
        [SerializeField] private CombatPlaygroundController controller;
        private CombatPlaygroundController _controller;
        private VisualElement _root;
        private VisualElement[] _panels;
        private Toggle _autoFireToggle;
        private Label _targetNameLabel;
        private Label _targetHealthLabel;
        private Label _targetMovementLabel;
        private Label _targetBuffLabel;
        private Label _energyLabel;
        private Label _attackLabel;
        private Label _projectileLabel;
        private Label _buffLabel;
        private Label _statusLabel;
        private Label _eventLogLabel;
        private Slider _damageSlider;
        private Slider _attackRateSlider;
        private Slider _rangeSlider;
        private Slider _projectileCountSlider;
        private Slider _penetrationSlider;
        private Slider _chargeSlider;
        private Slider _scalePunchSlider;
        private Slider _punchDurationSlider;
        private Slider _flashDurationSlider;
        private Button _fireButton;
        private Button _applyBuffButton;
        private Button _removeBuffButton;
        private Button _resetTargetButton;
        private Button _playTargetJuiceButton;
        private Button[] _projectileButtons;
        private Button[] _skillButtons;
        private Button[] _stageButtons;
        private System.Action[] _projectileHandlers;
        private System.Action[] _skillHandlers;
        private System.Action[] _stageHandlers;
        private System.Action _flashWhiteHandler;
        private System.Action _flashOrangeHandler;
        private System.Action _flashCyanHandler;
        private bool _bound;

        /// <summary>接收由场景作用域注入的控制器。</summary>
        [Inject]
        private void Construct(CombatPlaygroundController controller)
        {
            _controller = controller;
        }

        /// <summary>缓存 UIDocument 并等待视觉树完成创建。</summary>
        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            _controller = controller;
        }

        /// <summary>绑定视觉树控件并连接场景控制器。</summary>
        private void Start()
        {
            BindUi();
            _controller?.AttachView(this);
        }

        /// <summary>解除所有 UI 事件，避免场景卸载后保留控制器引用。</summary>
        private void OnDestroy()
        {
            CancelInvoke(nameof(BindUi));
            if (!_bound)
            {
                return;
            }

            _autoFireToggle?.UnregisterValueChangedCallback(OnAutoFireChanged);
            _damageSlider?.UnregisterValueChangedCallback(OnDamageChanged);
            _attackRateSlider?.UnregisterValueChangedCallback(OnAttackRateChanged);
            _rangeSlider?.UnregisterValueChangedCallback(OnRangeChanged);
            _projectileCountSlider?.UnregisterValueChangedCallback(OnProjectileCountChanged);
            _penetrationSlider?.UnregisterValueChangedCallback(OnPenetrationChanged);
            _chargeSlider?.UnregisterValueChangedCallback(OnChargeChanged);
            _scalePunchSlider?.UnregisterValueChangedCallback(OnScalePunchChanged);
            _punchDurationSlider?.UnregisterValueChangedCallback(OnPunchDurationChanged);
            _flashDurationSlider?.UnregisterValueChangedCallback(OnFlashDurationChanged);
            _fireButton.clicked -= OnFireClicked;
            _applyBuffButton.clicked -= OnApplyBuffClicked;
            _removeBuffButton.clicked -= OnRemoveBuffClicked;
            _resetTargetButton.clicked -= OnResetTargetClicked;
            _playTargetJuiceButton.clicked -= OnPlayTargetJuiceClicked;

            for (int index = 0; index < _projectileButtons.Length; index++)
            {
                _projectileButtons[index].clicked -= _projectileHandlers[index];
            }
            for (int index = 0; index < _skillButtons.Length; index++)
            {
                _skillButtons[index].clicked -= _skillHandlers[index];
            }
            for (int index = 0; index < _stageButtons.Length; index++)
            {
                _stageButtons[index].clicked -= _stageHandlers[index];
            }
            _root.Q<Button>("flashWhiteButton").clicked -= _flashWhiteHandler;
            _root.Q<Button>("flashOrangeButton").clicked -= _flashOrangeHandler;
            _root.Q<Button>("flashCyanButton").clicked -= _flashCyanHandler;
        }

        /// <summary>刷新四个面板显示的场景快照。</summary>
        internal void Refresh(in CombatPlaygroundSnapshot snapshot)
        {
            if (!_bound)
            {
                return;
            }

            _targetNameLabel.text = snapshot.HasTarget
                ? $"已选中：{snapshot.TargetName}"
                : $"目标：{snapshot.TargetName}（未选中）";
            _targetHealthLabel.text =
                $"生命：{snapshot.TargetHealth:0.##} / {snapshot.TargetMaxHealth:0.##}";
            _targetMovementLabel.text = $"移动倍率：{snapshot.MovementMultiplier:0.##}x";
            _targetBuffLabel.text = $"Buff：{snapshot.BuffName}";
            _energyLabel.text = $"能量：{snapshot.Energy:0.##} / {snapshot.MaximumEnergy:0.##}";
            _attackLabel.text = $"攻击提交：{snapshot.ShotCount}";
            _projectileLabel.text = $"子弹：{snapshot.ProjectileName}";
            _buffLabel.text = $"当前 Buff：{snapshot.BuffName}";
            _statusLabel.text = snapshot.Status;
            _eventLogLabel.text = snapshot.EventLog;
            _autoFireToggle.SetValueWithoutNotify(snapshot.AutoFire);

            _targetNameLabel.EnableInClassList("target-selected", snapshot.HasTarget);
            for (int index = 0; index < _skillButtons.Length; index++)
            {
                _skillButtons[index].SetEnabled(index > 0 && index < snapshot.SkillCount);
            }
        }

        /// <summary>判断屏幕坐标是否位于四个 UI 面板内。</summary>
        internal bool IsPointerOverUi(Vector2 screenPosition)
        {
            Vector2 panelPosition = new(screenPosition.x, Screen.height - screenPosition.y);
            for (int index = 0; index < _panels.Length; index++)
            {
                if (_panels[index].worldBound.Contains(panelPosition))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>绑定 UXML 中预先声明的控件。</summary>
        private void BindUi()
        {
            if (_document == null || _document.rootVisualElement == null)
            {
                Invoke(nameof(BindUi), 0.1f);
                return;
            }

            _root = _document.rootVisualElement;

            _panels = new[]
            {
                _root.Q<VisualElement>("targetPanel"),
                _root.Q<VisualElement>("attackPanel"),
                _root.Q<VisualElement>("buffPanel"),
                _root.Q<VisualElement>("tuningPanel")
            };
            _targetNameLabel = _root.Q<Label>("targetNameLabel");
            _targetHealthLabel = _root.Q<Label>("targetHealthLabel");
            _targetMovementLabel = _root.Q<Label>("targetMovementLabel");
            _targetBuffLabel = _root.Q<Label>("targetBuffLabel");
            _energyLabel = _root.Q<Label>("energyLabel");
            _attackLabel = _root.Q<Label>("attackLabel");
            _projectileLabel = _root.Q<Label>("projectileLabel");
            _buffLabel = _root.Q<Label>("buffLabel");
            _statusLabel = _root.Q<Label>("statusLabel");
            _eventLogLabel = _root.Q<Label>("eventLogLabel");
            _autoFireToggle = _root.Q<Toggle>("autoFireToggle");
            _fireButton = _root.Q<Button>("fireButton");
            _applyBuffButton = _root.Q<Button>("applyBuffButton");
            _removeBuffButton = _root.Q<Button>("removeBuffButton");
            _resetTargetButton = _root.Q<Button>("resetTargetButton");
            _playTargetJuiceButton = _root.Q<Button>("playTargetJuiceButton");
            _damageSlider = _root.Q<Slider>("damageSlider");
            _attackRateSlider = _root.Q<Slider>("attackRateSlider");
            _rangeSlider = _root.Q<Slider>("rangeSlider");
            _projectileCountSlider = _root.Q<Slider>("projectileCountSlider");
            _penetrationSlider = _root.Q<Slider>("penetrationSlider");
            _chargeSlider = _root.Q<Slider>("chargeSlider");
            _scalePunchSlider = _root.Q<Slider>("scalePunchSlider");
            _punchDurationSlider = _root.Q<Slider>("punchDurationSlider");
            _flashDurationSlider = _root.Q<Slider>("flashDurationSlider");
            _projectileButtons = QueryButtons("projectileButton", 7);
            _skillButtons = QueryButtons("skillButton", 5);
            _stageButtons = new[]
            {
                _root.Q<Button>("chargeStageButton"),
                _root.Q<Button>("beforeStageButton"),
                _root.Q<Button>("afterStageButton"),
                _root.Q<Button>("hitStageButton"),
                _root.Q<Button>("expiredStageButton")
            };

            _autoFireToggle.RegisterValueChangedCallback(OnAutoFireChanged);
            _damageSlider.RegisterValueChangedCallback(OnDamageChanged);
            _attackRateSlider.RegisterValueChangedCallback(OnAttackRateChanged);
            _rangeSlider.RegisterValueChangedCallback(OnRangeChanged);
            _projectileCountSlider.RegisterValueChangedCallback(OnProjectileCountChanged);
            _penetrationSlider.RegisterValueChangedCallback(OnPenetrationChanged);
            _chargeSlider.RegisterValueChangedCallback(OnChargeChanged);
            _scalePunchSlider.RegisterValueChangedCallback(OnScalePunchChanged);
            _punchDurationSlider.RegisterValueChangedCallback(OnPunchDurationChanged);
            _flashDurationSlider.RegisterValueChangedCallback(OnFlashDurationChanged);
            _fireButton.clicked += OnFireClicked;
            _applyBuffButton.clicked += OnApplyBuffClicked;
            _removeBuffButton.clicked += OnRemoveBuffClicked;
            _resetTargetButton.clicked += OnResetTargetClicked;
            _playTargetJuiceButton.clicked += OnPlayTargetJuiceClicked;

            BindProjectileButtons();
            BindSkillButtons();
            BindStageButtons();
            BindColorButtons();
            _bound = true;
            _controller?.AttachView(this);
        }

        /// <summary>查询一组预先声明的按钮。</summary>
        private Button[] QueryButtons(string prefix, int count)
        {
            Button[] buttons = new Button[count];
            for (int index = 0; index < count; index++)
            {
                buttons[index] = _root.Q<Button>($"{prefix}{index}");
            }

            return buttons;
        }

        /// <summary>绑定子弹选择按钮。</summary>
        private void BindProjectileButtons()
        {
            _projectileHandlers = new System.Action[_projectileButtons.Length];
            for (int index = 0; index < _projectileButtons.Length; index++)
            {
                int buttonIndex = index;
                _projectileHandlers[index] = () => _controller.SelectProjectile(buttonIndex);
                _projectileButtons[index].clicked += _projectileHandlers[index];
            }
        }

        /// <summary>绑定技能卡按钮。</summary>
        private void BindSkillButtons()
        {
            _skillHandlers = new System.Action[_skillButtons.Length];
            for (int index = 0; index < _skillButtons.Length; index++)
            {
                int buttonIndex = index;
                _skillHandlers[index] = () => _controller.ExecuteSkill(buttonIndex);
                _skillButtons[index].clicked += _skillHandlers[index];
            }
        }

        /// <summary>绑定蓄力、发射前、发射后、命中和过期按钮。</summary>
        private void BindStageButtons()
        {
            _stageHandlers = new System.Action[_stageButtons.Length];
            for (int index = 0; index < _stageButtons.Length; index++)
            {
                int buttonIndex = index;
                _stageHandlers[index] = () =>
                    _controller.PlayStage((CombatPlaygroundStage)buttonIndex);
                _stageButtons[index].clicked += _stageHandlers[index];
            }
        }

        /// <summary>绑定三种命中闪烁颜色按钮。</summary>
        private void BindColorButtons()
        {
            _flashWhiteHandler = () => _controller.SetFlashColor(Color.white);
            _flashOrangeHandler = () =>
                _controller.SetFlashColor(new Color(1f, 0.38f, 0.16f, 1f));
            _flashCyanHandler = () => _controller.SetFlashColor(Color.cyan);
            _root.Q<Button>("flashWhiteButton").clicked += _flashWhiteHandler;
            _root.Q<Button>("flashOrangeButton").clicked += _flashOrangeHandler;
            _root.Q<Button>("flashCyanButton").clicked += _flashCyanHandler;
        }

        private void OnAutoFireChanged(ChangeEvent<bool> change) => _controller.SetAutoFire(change.newValue);

        private void OnDamageChanged(ChangeEvent<float> change) => _controller.SetBaseDamage(change.newValue);

        private void OnAttackRateChanged(ChangeEvent<float> change) => _controller.SetAttacksPerSecond(change.newValue);

        private void OnRangeChanged(ChangeEvent<float> change) => _controller.SetBaseRange(change.newValue);

        private void OnProjectileCountChanged(ChangeEvent<float> change) => _controller.SetProjectileCount(change.newValue);

        private void OnPenetrationChanged(ChangeEvent<float> change) => _controller.SetPenetration(change.newValue);

        private void OnChargeChanged(ChangeEvent<float> change) => _controller.SetChargeDuration(change.newValue);

        private void OnScalePunchChanged(ChangeEvent<float> change) => _controller.SetScalePunch(change.newValue);

        private void OnPunchDurationChanged(ChangeEvent<float> change) => _controller.SetPunchDuration(change.newValue);

        private void OnFlashDurationChanged(ChangeEvent<float> change) => _controller.SetFlashDuration(change.newValue);

        private void OnFireClicked() => _controller.TryFireOnce();

        private void OnApplyBuffClicked() => _controller.ApplyBuff();

        private void OnRemoveBuffClicked() => _controller.RemoveBuff();

        private void OnResetTargetClicked() => _controller.ResetTarget();

        private void OnPlayTargetJuiceClicked() => _controller.PlayTargetJuice();
    }
}
