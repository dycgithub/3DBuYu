using System;
using System.Collections.Generic;
using CombatSystem;
using EnemySystem;
using GameSystem;
using Interfaces;
using Services;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;
using _Project.Tests.CombatPlayground.Model;
using _Project.Tests.CombatPlayground.View;

namespace _Project.Tests.CombatPlayground.Implement
{
    /// <summary>
    /// 编排 CombatPlayground 的目标选择、攻击、技能、Buff 与会话内弹道调参。
    /// </summary>
    public sealed class CombatPlaygroundController : MonoBehaviour
    {
        private const int PortIndex = 0;
        private const float RaycastDistance = 200f;
        private const string EnemyLayerName = "Enemy";

        [Header("场景对象")]
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private Enemy target;
        [SerializeField] private CombatPlaygroundTargetSet targetSet;
        [SerializeField] private CombatPlaygroundSkillRunner skillRunner;
        [SerializeField] private CombatPlaygroundEffectPreview effectPreview;
        [SerializeField] private CombatPlaygroundEnemyAttributes enemyAttributes;
        [SerializeField] private Transform firePoint;
        [SerializeField] private Play.TransmitterSO transmitter;
        [SerializeField] private ProjectileProfileSO defaultProjectile;
        [SerializeField] private ProjectileProfileSO[] projectileProfiles;
        [SerializeField] private BuffDefinitionSO buffDefinition;
        [SerializeField] private CombatPlaygroundJuice juice;

        [Header("会话参数")]
        [SerializeField] private CombatPlaygroundTuning tuning = new();

        [Inject] private ITransmitterAttackService _attackService;
        [Inject] private IBuffService _buffService;
        [Inject] private IEnergyService _energyService;
        [Inject] private ITransmitterGlobalEffectService _globalEffects;
        [Inject] private ICentralSkillEffectResetService _skillEffectReset;
        [Inject] private IProjectileHitEventSource _projectileEvents;

        private CombatPlaygroundView _view;
        private ProjectileProfileSO _activeProjectile;
        private readonly List<ProjectileProfileSO> _projectileCopies = new();
        private int _shotCount;
        private bool _autoFire;
        private bool _hasSelectedTarget;
        private CombatPlaygroundSkillExecutionMode _skillExecutionMode =
            CombatPlaygroundSkillExecutionMode.FormalRelease;
        private float _nextShotTime;
        private int _buffSourceId;
        private string _status = "等待点击敌人";
        private readonly List<string> _eventLog = new();

        /// <summary>初始化能量、投射物副本和真实投射物事件订阅。</summary>
        private void Start()
        {
            _buffSourceId = GetInstanceID();
            _energyService?.Initialize(100f, 100f);
            SelectProjectile(0, CombatPlaygroundStatusUpdateMode.Silent);
            if (_projectileEvents != null)
            {
                _projectileEvents.HitProcessed += OnProjectileHit;
            }
            RefreshView();
        }

        /// <summary>处理鼠标选敌和自动发射循环。</summary>
        private void Update()
        {
            SelectByMouse();
            if (!_autoFire || !_hasSelectedTarget || target == null || target.IsDead)
            {
                return;
            }

            _nextShotTime -= Time.unscaledDeltaTime;
            if (_nextShotTime > 0f)
            {
                return;
            }

            TryFireOnce();
            float multiplier = _globalEffects != null
                ? Mathf.Max(0.001f, _globalEffects.AttackSpeedMultiplier)
                : 1f;
            _nextShotTime = 1f / Mathf.Max(0.001f, tuning.AttacksPerSecond * multiplier);
        }

        /// <summary>解除真实投射物事件订阅并销毁会话内投射物副本。</summary>
        private void OnDestroy()
        {
            if (_projectileEvents != null)
            {
                _projectileEvents.HitProcessed -= OnProjectileHit;
            }

            for (int index = 0; index < _projectileCopies.Count; index++)
            {
                if (_projectileCopies[index] != null)
                {
                    Destroy(_projectileCopies[index]);
                }
            }

            _projectileCopies.Clear();
        }

        /// <summary>接收 UI Toolkit 视图。</summary>
        internal void AttachView(CombatPlaygroundView view)
        {
            _view = view;
            RefreshView();
        }

        /// <summary>根据鼠标位置选择场景中唯一的敌人。</summary>
        private void SelectByMouse()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame || sceneCamera == null)
            {
                return;
            }

            Vector2 screenPosition = mouse.position.ReadValue();
            if (_view != null && _view.IsPointerOverUi(screenPosition))
            {
                return;
            }

            Ray ray = sceneCamera.ScreenPointToRay(screenPosition);
            int enemyLayer = LayerMask.NameToLayer(EnemyLayerName);
            if (enemyLayer < 0 || !Physics.Raycast(
                    ray,
                    out RaycastHit hit,
                    RaycastDistance,
                    1 << enemyLayer,
                    QueryTriggerInteraction.Ignore))
            {
                return;
            }

            Enemy clickedEnemy = hit.collider.GetComponentInParent<Enemy>();
            if (targetSet != null && targetSet.Contains(clickedEnemy))
            {
                target = clickedEnemy;
                _hasSelectedTarget = true;
                SetStatus($"已选中：{target.name}");
            }
        }

        /// <summary>切换自动发射状态。</summary>
        internal void SetAutoFire(CombatPlaygroundAutoFireState state)
        {
            _autoFire = state == CombatPlaygroundAutoFireState.Enabled;
            _nextShotTime = 0f;
            SetStatus(_autoFire ? "自动发射已开启" : "自动发射已关闭");
        }

        /// <summary>尝试向当前选中的敌人发射一次。</summary>
        internal bool TryFireOnce()
        {
            if (!_hasSelectedTarget || target == null || target.IsDead)
            {
                SetStatus("射击失败：请先点击存活的敌人");
                return false;
            }

            if (_attackService == null || _activeProjectile == null || firePoint == null || transmitter == null)
            {
                SetStatus("射击失败：发射器配置不完整");
                return false;
            }

            Vector3 direction = target.transform.position - firePoint.position;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                SetStatus("射击失败：发射点与敌人位置重合");
                return false;
            }

            firePoint.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            juice?.PlayCharge(tuning.ChargeDuration);
            juice?.PlayBeforeFire();

            TransmitterAttackInput input = new(
                GetInstanceID(),
                PortIndex,
                _activeProjectile,
                firePoint.position,
                direction,
                tuning.BaseDamage,
                tuning.BaseRange,
                tuning.AttacksPerSecond,
                tuning.ProjectileCount,
                tuning.Penetration,
                transmitter.criticalChance,
                transmitter.criticalDamage,
                transmitter.portId,
                transmitter.fireSound,
                transmitter.fireEffectId);
            bool succeeded = _attackService.TryExecuteAttack(in input);
            juice?.PlayAfterFire(succeeded
                ? CombatPlaygroundFireResult.Accepted
                : CombatPlaygroundFireResult.Rejected);
            if (succeeded)
            {
                _shotCount++;
                SetStatus("射击已提交");
            }
            else
            {
                SetStatus("射击失败：攻击服务拒绝请求");
            }

            return succeeded;
        }

        /// <summary>切换当前会话使用的投射物配置。</summary>
        internal void SelectProjectile(int index)
        {
            SelectProjectile(index, CombatPlaygroundStatusUpdateMode.Report);
        }

        /// <summary>切换投射物并按指定模式更新状态提示。</summary>
        private void SelectProjectile(int index, CombatPlaygroundStatusUpdateMode updateMode)
        {
            ProjectileProfileSO source = index >= 0 && projectileProfiles != null &&
                index < projectileProfiles.Length
                ? projectileProfiles[index]
                : defaultProjectile;
            if (source == null)
            {
                return;
            }

            ProjectileProfileSO next = CloneProjectile(source);
            _activeProjectile = next;
            _projectileCopies.Add(next);
            if (updateMode == CombatPlaygroundStatusUpdateMode.Report)
            {
                SetStatus($"已选择子弹：{source.name}");
            }
            else
            {
                RefreshView();
            }
        }

        /// <summary>执行选中的技能，并根据模式选择正式释放或动作直测。</summary>
        internal void ExecuteSkill(int index)
        {
            IReadOnlyList<IDamageable> targets = targetSet.GetLivingTargets();
            Vector3 sourcePosition = firePoint.position;
            SetStatus(skillRunner.Execute(index, sourcePosition, targets, _skillExecutionMode));
        }

        /// <summary>设置正式技能释放或动作直测模式。</summary>
        internal void SetSkillExecutionMode(CombatPlaygroundSkillExecutionMode mode)
        {
            _skillExecutionMode = mode;
            SetStatus(mode == CombatPlaygroundSkillExecutionMode.FormalRelease
                ? "技能模式：正式释放（含能量、冷却与次数）"
                : "技能模式：动作直测（绕过玩家释放规则）");
        }

        /// <summary>播放战斗特效目录中的独立预览项。</summary>
        internal void PlayEffectPreview(int index)
        {
            Transform parent = target != null ? target.transform : null;
            Vector3 position = parent != null
                ? parent.position
                : firePoint.position;
            SetStatus(effectPreview.Play(index, position, parent));
        }

        /// <summary>切换偏轴目标移动，观察追踪投射物转向。</summary>
        internal void ToggleTargetMotion()
        {
            targetSet.ToggleMotion();
            SetStatus("已切换追踪目标移动状态");
        }

        /// <summary>向当前敌人施加场景配置的 Buff。</summary>
        internal void ApplyBuff()
        {
            if (target == null || buffDefinition == null || target.IsDead)
            {
                SetStatus("Buff 失败：目标或 Buff 不可用");
                return;
            }

            target.ApplyBuff(buffDefinition, _buffSourceId);
            SetStatus($"已施加 Buff：{buffDefinition.name}");
        }

        /// <summary>移除当前实验场景来源施加的 Buff。</summary>
        internal void RemoveBuff()
        {
            if (target == null || _buffService == null)
            {
                return;
            }

            _buffService.RemoveBySourceId(target, _buffSourceId);
            SetStatus("已移除实验场景 Buff");
        }

        /// <summary>重置全部敌人、临时属性、技能和场景全局效果。</summary>
        internal void ResetTarget()
        {
            _autoFire = false;
            _hasSelectedTarget = true;
            enemyAttributes.ResetAll();
            targetSet.ResetAll();
            target = targetSet.PrimaryTarget;

            _skillEffectReset.ClearPermanentEffects();
            skillRunner.ResetSkillState();
            _energyService.Initialize(100f, 100f);
            juice?.ResetVisuals();
            SetStatus("全部目标、Buff、技能资源与全局效果已重置");
        }

        /// <summary>更新指定的会话内发射器或投射物参数。</summary>
        internal void SetAttackTuning(CombatPlaygroundAttackParameter parameter, float value)
        {
            switch (parameter)
            {
                case CombatPlaygroundAttackParameter.BaseDamage:
                    tuning.SetBaseDamage(value);
                    break;
                case CombatPlaygroundAttackParameter.AttackRate:
                    tuning.SetAttacksPerSecond(value);
                    break;
                case CombatPlaygroundAttackParameter.Range:
                    tuning.SetBaseRange(value);
                    break;
                case CombatPlaygroundAttackParameter.ProjectileCount:
                    tuning.SetProjectileCount(value);
                    break;
                case CombatPlaygroundAttackParameter.Penetration:
                    tuning.SetPenetration(value);
                    break;
                case CombatPlaygroundAttackParameter.ProjectileSpeed:
                    if (_activeProjectile != null)
                    {
                        _activeProjectile.SpeedUnitsPerSecond = Mathf.Max(0f, value);
                    }
                    break;
                case CombatPlaygroundAttackParameter.CollisionRadius:
                    if (_activeProjectile != null)
                    {
                        _activeProjectile.CollisionRadius = Mathf.Max(0f, value);
                    }
                    break;
            }

            RefreshView();
        }

        /// <summary>更新选中敌人的一个临时属性参数。</summary>
        internal void SetEnemyAttribute(CombatPlaygroundEnemyAttribute attribute, float value)
        {
            switch (attribute)
            {
                case CombatPlaygroundEnemyAttribute.HealthMultiplier:
                    enemyAttributes?.SetHealthMultiplier(value);
                    break;
                case CombatPlaygroundEnemyAttribute.MovementMultiplier:
                    enemyAttributes?.SetMovementMultiplier(value);
                    break;
                case CombatPlaygroundEnemyAttribute.DamageTakenMultiplier:
                    enemyAttributes?.SetDamageTakenMultiplier(value);
                    break;
                case CombatPlaygroundEnemyAttribute.AttackDamageMultiplier:
                    enemyAttributes?.SetAttackDamageMultiplier(value);
                    break;
                case CombatPlaygroundEnemyAttribute.AttackAvailabilityMultiplier:
                    enemyAttributes?.SetAttackAllowedMultiplier(value);
                    break;
                case CombatPlaygroundEnemyAttribute.BuffDuration:
                    enemyAttributes?.SetBuffDuration(value);
                    break;
            }

            RefreshView();
        }

        /// <summary>将面板中的会话属性应用到当前选中敌人。</summary>
        internal void ApplyEnemyAttributes()
        {
            if (enemyAttributes == null || target == null)
            {
                SetStatus("应用属性失败：请先选中场景敌人");
                return;
            }

            bool applied = enemyAttributes.Apply(target, _buffService);
            bool behaviorReady = targetSet == null || targetSet.RefreshBehaviorAgent(target);

            SetStatus(!applied
                ? "应用属性失败：Buff 服务不可用"
                : behaviorReady
                    ? $"已应用临时属性：{target.name}"
                    : $"已应用临时属性：{target.name}；Shooter ECS Agent 未启动");
        }

        /// <summary>接收真实投射物命中事件并记录结果。</summary>
        private void OnProjectileHit(ProjectileHitContext context)
        {
            if (context.Target is not Enemy hitEnemy ||
                (targetSet != null && !targetSet.Contains(hitEnemy)))
            {
                return;
            }

            SetStatus(context.DamageResult.WasApplied
                ? $"投射物命中 {hitEnemy.name}：伤害 {context.DamageResult.AppliedDamage:0.##}"
                : $"投射物命中 {hitEnemy.name}，但伤害未生效");
        }

        /// <summary>克隆完整投射物配置，避免遗漏高级弹道字段或写回共享资产。</summary>
        private static ProjectileProfileSO CloneProjectile(ProjectileProfileSO source)
        {
            ProjectileProfileSO clone = Instantiate(source);
            clone.name = $"CombatPlayground_{source.name}";
            return clone;
        }

        /// <summary>设置状态文本并保留最近的八条事件。</summary>
        private void SetStatus(string message)
        {
            _status = message;
            _eventLog.Add(message);
            if (_eventLog.Count > 8)
            {
                _eventLog.RemoveAt(0);
            }
            RefreshView();
        }

        /// <summary>生成当前场景状态并刷新 UI。</summary>
        private void RefreshView()
        {
            float energy = _energyService?.CurrentEnergy != null
                ? _energyService.CurrentEnergy.CurrentValue
                : 0f;
            string log = string.Join("\n", _eventLog);
            CombatPlaygroundSnapshot snapshot = new(
                target != null ? target.EnemyType.ToString() : "未配置",
                target != null ? target.CurrentHealth : 0f,
                target != null ? target.MaxHealth : 0f,
                target != null
                    ? target.SpeedMultiplier * (_buffService?.GetModifierMultiplier(
                        target,
                        BuffModifierType.MovementSpeedMultiplier) ?? 1f)
                    : 1f,
                energy,
                _energyService?.MaximumEnergy ?? 0f,
                _hasSelectedTarget,
                _autoFire,
                _skillExecutionMode,
                enemyAttributes != null ? enemyAttributes.Snapshot : default,
                _shotCount,
                skillRunner != null ? skillRunner.SkillCount : 0,
                _activeProjectile != null ? _activeProjectile.name : "未配置",
                _activeProjectile != null ? _activeProjectile.SpeedUnitsPerSecond : 0f,
                _activeProjectile != null ? _activeProjectile.CollisionRadius : 0f,
                buffDefinition != null ? buffDefinition.name : "未配置",
                _status,
                log);
            _view?.Refresh(in snapshot);
        }
    }
}
