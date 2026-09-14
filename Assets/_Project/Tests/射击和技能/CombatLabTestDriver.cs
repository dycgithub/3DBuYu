using System;
using System.Collections.Generic;
using CombatSystem;
using EnemySystem;
using Interfaces;
using Play;
using Services;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

/// <summary>
/// 射击和技能测试场景的最小操作入口。
/// 场景通过 CombatLabLifetimeScope 的自动注入列表注入本组件，组件不创建运行时 UI。
/// </summary>
public sealed class CombatLabTestDriver : MonoBehaviour
{
    /// <summary>技能测试使用的目标选项。</summary>
    public enum SkillTargetOption
    {
        ConfiguredTarget,
        NoTarget
    }

    [Header("场景引用")]
    [SerializeField] private CentralCore _centralCore;
    [SerializeField] private Enemy _target;
    [SerializeField] private SkillDefinitionSO _skillDefinition;
    [SerializeField] private UIDocument _uiDocument;

    [Header("测试准备")]
    [Min(1f)] [SerializeField] private float _initialEnergy = 100f;

    [Inject] private ITransmitterAttackService _attackService;
    [Inject] private ISkillExecutionService _skillService;
    [Inject] private IEnergyService _energyService;

    private Label _statusLabel;
    private Label _targetHealthLabel;
    private Button _fireButton;
    private Button _skillButton;
    private Button _noTargetButton;
    private Button _noDefinitionButton;

    /// <summary>
    /// 准备测试能量并绑定场景中已配置的 UI 控件。
    /// </summary>
    private void Start()
    {
        if (_energyService != null)
        {
            _energyService.Initialize(_initialEnergy, _initialEnergy);
        }

        BindUi();
        RefreshTargetHealth();
    }

    /// <summary>
    /// 通过 ITransmitterAttackService 尝试提交第一个已解锁端口的一次射击。
    /// </summary>
    /// <returns>攻击服务接受请求时返回 true。</returns>
    public bool TryFireOnce()
    {
        if (!TryCreateAttackInput(out TransmitterAttackInput input))
        {
            SetStatus("射击失败：缺少可用端口或投射物定义");
            return false;
        }

        bool succeeded = _attackService != null && _attackService.TryExecuteAttack(in input);
        SetStatus(succeeded ? "射击已提交：投射物进入模拟管线" : "射击失败：攻击服务拒绝请求");
        return succeeded;
    }

    /// <summary>
    /// 仅用于测试：直接通过 ISkillExecutionService 执行当前技能定义。
    /// </summary>
    /// <param name="targetOption">使用场景目标或空目标列表。</param>
    /// <returns>技能执行结果。</returns>
    public SkillExecutionResult ExecuteSkillTest(SkillTargetOption targetOption)
    {
        SkillExecutionResult result = ExecuteSkill(_skillDefinition, targetOption);
        SetStatus(FormatSkillResult(result));
        RefreshTargetHealth();
        return result;
    }

    /// <summary>
    /// 使用空技能定义执行一次，验证缺少定义时返回失败结果。
    /// </summary>
    /// <returns>技能执行结果。</returns>
    public SkillExecutionResult ExecuteSkillNoDefinition()
    {
        SkillExecutionResult result = ExecuteSkill(null, SkillTargetOption.NoTarget);
        SetStatus(FormatSkillResult(result));
        return result;
    }

    /// <summary>绑定 UXML 中预先配置的控件。</summary>
    private void BindUi()
    {
        if (_uiDocument == null)
        {
            _uiDocument = GetComponent<UIDocument>();
        }

        if (_uiDocument == null || _uiDocument.rootVisualElement == null)
        {
            return;
        }

        VisualElement root = _uiDocument.rootVisualElement;
        _statusLabel = root.Q<Label>("status-label");
        _targetHealthLabel = root.Q<Label>("target-health-label");
        _fireButton = root.Q<Button>("fire-button");
        _skillButton = root.Q<Button>("skill-button");
        _noTargetButton = root.Q<Button>("no-target-button");
        _noDefinitionButton = root.Q<Button>("no-definition-button");

        if (_fireButton != null)
        {
            _fireButton.clicked += OnFireClicked;
        }

        if (_skillButton != null)
        {
            _skillButton.clicked += OnSkillClicked;
        }

        if (_noTargetButton != null)
        {
            _noTargetButton.clicked += OnNoTargetClicked;
        }

        if (_noDefinitionButton != null)
        {
            _noDefinitionButton.clicked += OnNoDefinitionClicked;
        }
    }

    /// <summary>解除 UI 回调，避免场景卸载后保留组件引用。</summary>
    private void OnDestroy()
    {
        if (_fireButton != null)
        {
            _fireButton.clicked -= OnFireClicked;
        }

        if (_skillButton != null)
        {
            _skillButton.clicked -= OnSkillClicked;
        }

        if (_noTargetButton != null)
        {
            _noTargetButton.clicked -= OnNoTargetClicked;
        }

        if (_noDefinitionButton != null)
        {
            _noDefinitionButton.clicked -= OnNoDefinitionClicked;
        }
    }

    /// <summary>响应一次射击按钮。</summary>
    private void OnFireClicked()
    {
        TryFireOnce();
    }

    /// <summary>响应带场景目标的技能按钮。</summary>
    private void OnSkillClicked()
    {
        ExecuteSkillTest(SkillTargetOption.ConfiguredTarget);
    }

    /// <summary>响应无目标技能按钮。</summary>
    private void OnNoTargetClicked()
    {
        ExecuteSkillTest(SkillTargetOption.NoTarget);
    }

    /// <summary>响应无定义技能按钮。</summary>
    private void OnNoDefinitionClicked()
    {
        ExecuteSkillNoDefinition();
    }

    /// <summary>从第一个端口读取输入，缺少任何必需引用时返回 false。</summary>
    private bool TryCreateAttackInput(out TransmitterAttackInput input)
    {
        input = default;
        if (_centralCore == null || _centralCore.PortCount == 0)
        {
            return false;
        }

        CentralTransmitterRuntime port = _centralCore.GetPort(0);
        if (port == null || port.IsLocked || port.So == null || port.FirePoint == null)
        {
            return false;
        }

        ProjectileProfileSO profile = port.So.defaultProjectileProfile;
        if (profile == null)
        {
            return false;
        }

        Transform firePoint = port.FirePoint;
        input = new TransmitterAttackInput(
            _centralCore.GetInstanceID(),
            port.PortIndex,
            profile,
            firePoint.position,
            firePoint.forward,
            port.So.baseDamage,
            port.So.baseRange,
            port.So.attacksPerSecond,
            port.So.projectileCount,
            port.So.penetration,
            port.So.criticalChance,
            port.So.criticalDamage,
            port.PortId);
        return true;
    }

    /// <summary>创建技能请求并把目标快照交给技能执行边界。</summary>
    private SkillExecutionResult ExecuteSkill(
        SkillDefinitionSO definition,
        SkillTargetOption targetOption)
    {
        if (_skillService == null)
        {
            return SkillExecutionResult.Failure(SkillExecutionFailureReason.ActionFailed);
        }

        IReadOnlyList<IDamageable> targets = targetOption == SkillTargetOption.ConfiguredTarget &&
            _target != null
            ? new IDamageable[] { _target }
            : Array.Empty<IDamageable>();
        int sourceInstanceId = _centralCore != null
            ? _centralCore.GetInstanceID()
            : GetInstanceID();
        SkillExecutionRequest request = new(
            definition,
            sourceInstanceId,
            transform.position,
            targets);
        return _skillService.Execute(in request);
    }

    /// <summary>显示技能结果的稳定文本，不把失败当作异常处理。</summary>
    private static string FormatSkillResult(SkillExecutionResult result)
    {
        return result.Succeeded
            ? $"技能已执行：命中 {result.AppliedTargetCount} 个目标"
            : $"技能失败：{result.FailureReason}";
    }

    /// <summary>刷新测试敌人的当前生命值。</summary>
    private void RefreshTargetHealth()
    {
        if (_targetHealthLabel == null)
        {
            return;
        }

        _targetHealthLabel.text = _target == null
            ? "测试目标：未配置"
            : $"测试目标生命：{Mathf.Max(0f, _target.CurrentHealth):0.##}/{Mathf.Max(0f, _target.MaxHealth):0.##}";
    }

    /// <summary>更新状态标签。</summary>
    private void SetStatus(string message)
    {
        if (_statusLabel != null)
        {
            _statusLabel.text = message;
        }
    }
}
