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
        private Toggle _formalSkillToggle;
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
        private Slider _projectileSpeedSlider;
        private Slider _collisionRadiusSlider;
        private Slider _healthMultiplierSlider;
        private Slider _enemyMovementSlider;
        private Slider _damageTakenMultiplierSlider;
        private Slider _attackDamageMultiplierSlider;
        private Toggle _enemyAttackEnabledToggle;
        private Slider _attributeDurationSlider;
        private Button _fireButton;
        private Button _applyBuffButton;
        private Button _removeBuffButton;
        private Button _resetTargetButton;
        private Button _targetMotionButton;
        private Button _applyAttributesButton;
        private Button[] _projectileButtons;
        private Button[] _skillButtons;
        private Button[] _effectButtons;
        private System.Action[] _projectileHandlers;
        private System.Action[] _skillHandlers;
        private System.Action[] _effectHandlers;
        private System.Action _targetMotionHandler;
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
            _formalSkillToggle?.UnregisterValueChangedCallback(OnFormalSkillChanged);
            _damageSlider?.UnregisterValueChangedCallback(OnDamageChanged);
            _attackRateSlider?.UnregisterValueChangedCallback(OnAttackRateChanged);
            _rangeSlider?.UnregisterValueChangedCallback(OnRangeChanged);
            _projectileCountSlider?.UnregisterValueChangedCallback(OnProjectileCountChanged);
            _penetrationSlider?.UnregisterValueChangedCallback(OnPenetrationChanged);
            _projectileSpeedSlider?.UnregisterValueChangedCallback(OnProjectileSpeedChanged);
            _collisionRadiusSlider?.UnregisterValueChangedCallback(OnCollisionRadiusChanged);
            _healthMultiplierSlider?.UnregisterValueChangedCallback(OnHealthMultiplierChanged);
            _enemyMovementSlider?.UnregisterValueChangedCallback(OnEnemyMovementChanged);
            _damageTakenMultiplierSlider?.UnregisterValueChangedCallback(OnDamageTakenMultiplierChanged);
            _attackDamageMultiplierSlider?.UnregisterValueChangedCallback(OnAttackDamageMultiplierChanged);
            _enemyAttackEnabledToggle?.UnregisterValueChangedCallback(OnEnemyAttackToggled);
            _attributeDurationSlider?.UnregisterValueChangedCallback(OnAttributeDurationChanged);
            _fireButton.clicked -= OnFireClicked;
            _applyBuffButton.clicked -= OnApplyBuffClicked;
            _removeBuffButton.clicked -= OnRemoveBuffClicked;
            _resetTargetButton.clicked -= OnResetTargetClicked;
            _targetMotionButton.clicked -= _targetMotionHandler;
            _applyAttributesButton.clicked -= OnApplyAttributesClicked;

            for (int index = 0; index < _projectileButtons.Length; index++)
            {
                _projectileButtons[index].clicked -= _projectileHandlers[index];
            }
            for (int index = 0; index < _skillButtons.Length; index++)
            {
                _skillButtons[index].clicked -= _skillHandlers[index];
            }
            for (int index = 0; index < _effectButtons.Length; index++)
            {
                _effectButtons[index].clicked -= _effectHandlers[index];
            }
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
            _formalSkillToggle.SetValueWithoutNotify(
                snapshot.SkillExecutionMode == CombatPlaygroundSkillExecutionMode.FormalRelease);
            _projectileSpeedSlider.SetValueWithoutNotify(snapshot.ProjectileSpeed);
            _collisionRadiusSlider.SetValueWithoutNotify(snapshot.CollisionRadius);
            _healthMultiplierSlider.SetValueWithoutNotify(snapshot.EnemyTuning.HealthMultiplier);
            _enemyMovementSlider.SetValueWithoutNotify(snapshot.EnemyTuning.MovementMultiplier);
            _damageTakenMultiplierSlider.SetValueWithoutNotify(snapshot.EnemyTuning.DamageTakenMultiplier);
            _attackDamageMultiplierSlider.SetValueWithoutNotify(snapshot.EnemyTuning.AttackDamageMultiplier);
            _enemyAttackEnabledToggle.SetValueWithoutNotify(
                snapshot.EnemyTuning.AttackAvailabilityMultiplier > 0f);
            _attributeDurationSlider.SetValueWithoutNotify(snapshot.EnemyTuning.BuffDuration);

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

            VisualElement root = _document.rootVisualElement;
            if (!IsTreeReady(root))
            {
                Invoke(nameof(BindUi), 0.1f);
                return;
            }

            _root = root;

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
            _formalSkillToggle = _root.Q<Toggle>("formalSkillToggle");
            _fireButton = _root.Q<Button>("fireButton");
            _applyBuffButton = _root.Q<Button>("applyBuffButton");
            _removeBuffButton = _root.Q<Button>("removeBuffButton");
            _resetTargetButton = _root.Q<Button>("resetTargetButton");
            _targetMotionButton = _root.Q<Button>("targetMotionButton");
            _damageSlider = _root.Q<Slider>("damageSlider");
            _attackRateSlider = _root.Q<Slider>("attackRateSlider");
            _rangeSlider = _root.Q<Slider>("rangeSlider");
            _projectileCountSlider = _root.Q<Slider>("projectileCountSlider");
            _penetrationSlider = _root.Q<Slider>("penetrationSlider");
            _projectileSpeedSlider = _root.Q<Slider>("projectileSpeedSlider");
            _collisionRadiusSlider = _root.Q<Slider>("collisionRadiusSlider");
            _healthMultiplierSlider = _root.Q<Slider>("healthMultiplierSlider");
            _enemyMovementSlider = _root.Q<Slider>("enemyMovementSlider");
            _damageTakenMultiplierSlider = _root.Q<Slider>("damageTakenMultiplierSlider");
            _attackDamageMultiplierSlider = _root.Q<Slider>("attackDamageMultiplierSlider");
            _enemyAttackEnabledToggle = _root.Q<Toggle>("enemyAttackEnabledToggle");
            _attributeDurationSlider = _root.Q<Slider>("attributeDurationSlider");
            _applyAttributesButton = _root.Q<Button>("applyAttributesButton");
            _projectileButtons = QueryButtons("projectileButton", 7);
            _skillButtons = QueryButtons("skillButton", 8);
            _effectButtons = QueryButtons("effectButton", 8);

            _autoFireToggle.RegisterValueChangedCallback(OnAutoFireChanged);
            _formalSkillToggle.RegisterValueChangedCallback(OnFormalSkillChanged);
            _damageSlider.RegisterValueChangedCallback(OnDamageChanged);
            _attackRateSlider.RegisterValueChangedCallback(OnAttackRateChanged);
            _rangeSlider.RegisterValueChangedCallback(OnRangeChanged);
            _projectileCountSlider.RegisterValueChangedCallback(OnProjectileCountChanged);
            _penetrationSlider.RegisterValueChangedCallback(OnPenetrationChanged);
            _projectileSpeedSlider.RegisterValueChangedCallback(OnProjectileSpeedChanged);
            _collisionRadiusSlider.RegisterValueChangedCallback(OnCollisionRadiusChanged);
            _healthMultiplierSlider.RegisterValueChangedCallback(OnHealthMultiplierChanged);
            _enemyMovementSlider.RegisterValueChangedCallback(OnEnemyMovementChanged);
            _damageTakenMultiplierSlider.RegisterValueChangedCallback(OnDamageTakenMultiplierChanged);
            _attackDamageMultiplierSlider.RegisterValueChangedCallback(OnAttackDamageMultiplierChanged);
            _enemyAttackEnabledToggle.RegisterValueChangedCallback(OnEnemyAttackToggled);
            _attributeDurationSlider.RegisterValueChangedCallback(OnAttributeDurationChanged);
            _fireButton.clicked += OnFireClicked;
            _applyBuffButton.clicked += OnApplyBuffClicked;
            _removeBuffButton.clicked += OnRemoveBuffClicked;
            _resetTargetButton.clicked += OnResetTargetClicked;
            _targetMotionHandler = _controller.ToggleTargetMotion;
            _targetMotionButton.clicked += _targetMotionHandler;
            _applyAttributesButton.clicked += OnApplyAttributesClicked;

            BindProjectileButtons();
            BindSkillButtons();
            BindEffectButtons();
            _bound = true;
            _controller?.AttachView(this);
        }

        private static bool IsTreeReady(VisualElement root)
        {
            return root.Q<Label>("targetNameLabel") != null &&
                root.Q<Toggle>("autoFireToggle") != null &&
                root.Q<Toggle>("formalSkillToggle") != null &&
                root.Q<Button>("fireButton") != null &&
                root.Q<Button>("applyBuffButton") != null &&
                root.Q<Button>("removeBuffButton") != null &&
                root.Q<Button>("resetTargetButton") != null &&
                root.Q<Toggle>("enemyAttackEnabledToggle") != null &&
                root.Q<Button>("targetMotionButton") != null &&
                root.Q<Slider>("projectileSpeedSlider") != null &&
                root.Q<Slider>("healthMultiplierSlider") != null &&
                root.Q<Button>("skillButton7") != null &&
                root.Q<Button>("effectButton7") != null;
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

        /// <summary>绑定目录特效独立预览按钮。</summary>
        private void BindEffectButtons()
        {
            _effectHandlers = new System.Action[_effectButtons.Length];
            for (int index = 0; index < _effectButtons.Length; index++)
            {
                int buttonIndex = index;
                _effectHandlers[index] = () => _controller.PlayEffectPreview(buttonIndex);
                _effectButtons[index].clicked += _effectHandlers[index];
            }
        }

        private void OnAutoFireChanged(ChangeEvent<bool> change) =>
            _controller.SetAutoFire(change.newValue
                ? CombatPlaygroundAutoFireState.Enabled
                : CombatPlaygroundAutoFireState.Disabled);

        private void OnFormalSkillChanged(ChangeEvent<bool> change) =>
            _controller.SetSkillExecutionMode(change.newValue
                ? CombatPlaygroundSkillExecutionMode.FormalRelease
                : CombatPlaygroundSkillExecutionMode.DirectAction);

        private void OnDamageChanged(ChangeEvent<float> change) =>
            _controller.SetAttackTuning(CombatPlaygroundAttackParameter.BaseDamage, change.newValue);

        private void OnAttackRateChanged(ChangeEvent<float> change) =>
            _controller.SetAttackTuning(CombatPlaygroundAttackParameter.AttackRate, change.newValue);

        private void OnRangeChanged(ChangeEvent<float> change) =>
            _controller.SetAttackTuning(CombatPlaygroundAttackParameter.Range, change.newValue);

        private void OnProjectileCountChanged(ChangeEvent<float> change) =>
            _controller.SetAttackTuning(CombatPlaygroundAttackParameter.ProjectileCount, change.newValue);

        private void OnPenetrationChanged(ChangeEvent<float> change) =>
            _controller.SetAttackTuning(CombatPlaygroundAttackParameter.Penetration, change.newValue);

        private void OnProjectileSpeedChanged(ChangeEvent<float> change) =>
            _controller.SetAttackTuning(CombatPlaygroundAttackParameter.ProjectileSpeed, change.newValue);

        private void OnCollisionRadiusChanged(ChangeEvent<float> change) =>
            _controller.SetAttackTuning(CombatPlaygroundAttackParameter.CollisionRadius, change.newValue);

        private void OnHealthMultiplierChanged(ChangeEvent<float> change) =>
            _controller.SetEnemyAttribute(CombatPlaygroundEnemyAttribute.HealthMultiplier, change.newValue);

        private void OnEnemyMovementChanged(ChangeEvent<float> change) =>
            _controller.SetEnemyAttribute(CombatPlaygroundEnemyAttribute.MovementMultiplier, change.newValue);

        private void OnDamageTakenMultiplierChanged(ChangeEvent<float> change) =>
            _controller.SetEnemyAttribute(CombatPlaygroundEnemyAttribute.DamageTakenMultiplier, change.newValue);

        private void OnAttackDamageMultiplierChanged(ChangeEvent<float> change) =>
            _controller.SetEnemyAttribute(CombatPlaygroundEnemyAttribute.AttackDamageMultiplier, change.newValue);

        private void OnEnemyAttackToggled(ChangeEvent<bool> change) =>
            _controller.SetEnemyAttribute(
                CombatPlaygroundEnemyAttribute.AttackAvailabilityMultiplier,
                change.newValue ? 1f : 0f);

        private void OnAttributeDurationChanged(ChangeEvent<float> change) =>
            _controller.SetEnemyAttribute(CombatPlaygroundEnemyAttribute.BuffDuration, change.newValue);

        private void OnFireClicked() => _controller.TryFireOnce();

        private void OnApplyBuffClicked() => _controller.ApplyBuff();

        private void OnRemoveBuffClicked() => _controller.RemoveBuff();

        private void OnResetTargetClicked() => _controller.ResetTarget();

        private void OnApplyAttributesClicked() => _controller.ApplyEnemyAttributes();

    }
}
