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
    /// 编排 CombatPlayground 的目标选择、攻击、技能、Buff 和运行时调参。
    /// </summary>
    public sealed class CombatPlaygroundController : MonoBehaviour
    {
        private const int PortIndex = 0;
        private const float RaycastDistance = 200f;

        [Header("场景对象")]
        [SerializeField] private Camera sceneCamera;
        [SerializeField] private Enemy target;
        [SerializeField] private Transform firePoint;
        [SerializeField] private Play.TransmitterSO transmitter;
        [SerializeField] private ProjectileProfileSO defaultProjectile;
        [SerializeField] private ProjectileProfileSO[] projectileProfiles;
        [SerializeField] private SkillDefinitionSO[] skills;
        [SerializeField] private BuffDefinitionSO buffDefinition;
        [SerializeField] private CombatPlaygroundJuice juice;

        [Header("会话参数")]
        [SerializeField] private CombatPlaygroundTuning tuning = new();

        [Inject] private ITransmitterAttackService _attackService;
        [Inject] private ISkillExecutionService _skillService;
        [Inject] private IBuffService _buffService;
        [Inject] private IEnergyService _energyService;
        [Inject] private ITransmitterGlobalEffectService _globalEffects;
        [Inject] private IProjectileHitEventSource _projectileEvents;

        private CombatPlaygroundView _view;
        private ProjectileProfileSO _activeProjectile;
        private int _projectileIndex;
        private int _shotCount;
        private bool _autoFire;
        private bool _hasSelectedTarget;
        private float _nextShotTime;
        private int _buffSourceId;
        private string _status = "等待点击敌人";
        private readonly List<string> _eventLog = new();

        /// <summary>初始化能量、投射物副本和真实投射物事件订阅。</summary>
        private void Start()
        {
            _buffSourceId = GetInstanceID();
            _energyService?.Initialize(100f, 100f);
            SelectProjectile(0, false);
            _projectileEvents.HitProcessed += OnProjectileHit;
            ApplyJuiceTuning();
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

            if (_activeProjectile != null)
            {
                Destroy(_activeProjectile);
            }
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
            if (!Physics.Raycast(ray, out RaycastHit hit, RaycastDistance))
            {
                return;
            }

            Enemy clickedEnemy = hit.collider.GetComponentInParent<Enemy>();
            if (clickedEnemy == target)
            {
                _hasSelectedTarget = true;
                SetStatus("已选中敌人");
            }
        }

        /// <summary>切换自动发射状态。</summary>
        internal void SetAutoFire(bool enabled)
        {
            _autoFire = enabled;
            _nextShotTime = 0f;
            SetStatus(enabled ? "自动发射已开启" : "自动发射已关闭");
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
            juice?.PlayAfterFire(succeeded);
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
            SelectProjectile(index, true);
        }

        /// <summary>切换投射物并控制是否刷新操作状态。</summary>
        private void SelectProjectile(int index, bool updateStatus)
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
            if (_activeProjectile != null)
            {
                Destroy(_activeProjectile);
            }

            _activeProjectile = next;
            _projectileIndex = Mathf.Max(0, index);
            if (updateStatus)
            {
                SetStatus($"已选择子弹：{source.name}");
            }
            else
            {
                RefreshView();
            }
        }

        /// <summary>执行选中的技能卡并把当前敌人作为显式目标。</summary>
        internal void ExecuteSkill(int index)
        {
            if (index == 0)
            {
                SetStatus("技能不可用：场景只有一个发射器");
                return;
            }

            if (_skillService == null || target == null || target.IsDead ||
                skills == null || index < 0 || index >= skills.Length || skills[index] == null)
            {
                SetStatus("技能失败：技能或目标不可用");
                return;
            }

            IReadOnlyList<IDamageable> targets = new IDamageable[] { target };
            SkillExecutionRequest request = new(
                skills[index],
                GetInstanceID(),
                firePoint != null ? firePoint.position : transform.position,
                targets);
            SkillExecutionResult result = _skillService.Execute(in request);
            SetStatus(result.Succeeded
                ? $"技能已执行：{skills[index].SkillIdentifier}"
                : $"技能失败：{result.FailureReason}");
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

        /// <summary>重置唯一敌人的生命、Buff、速度和表现状态。</summary>
        internal void ResetTarget()
        {
            _autoFire = false;
            _hasSelectedTarget = true;
            target?.ResetForReuse();
            target?.InitializeForSpawn(1f, 1f);
            juice?.ResetVisuals();
            SetStatus("敌人已重置");
        }

        /// <summary>播放一个手动选择的 Juice 阶段。</summary>
        internal void PlayStage(CombatPlaygroundStage stage)
        {
            switch (stage)
            {
                case CombatPlaygroundStage.Charge:
                    juice?.PlayCharge(tuning.ChargeDuration);
                    break;
                case CombatPlaygroundStage.BeforeFire:
                    juice?.PlayBeforeFire();
                    break;
                case CombatPlaygroundStage.AfterFire:
                    juice?.PlayAfterFire(true);
                    break;
                case CombatPlaygroundStage.Hit:
                    juice?.PlayTargetHit();
                    break;
                case CombatPlaygroundStage.Expired:
                    juice?.PlayTargetDeath();
                    break;
            }

            SetStatus($"已播放阶段：{stage}");
        }

        /// <summary>更新攻击调参并同步 Juice 参数。</summary>
        internal void SetBaseDamage(float value)
        {
            tuning.SetBaseDamage(value);
            RefreshView();
        }

        /// <summary>更新攻击频率。</summary>
        internal void SetAttacksPerSecond(float value)
        {
            tuning.SetAttacksPerSecond(value);
            RefreshView();
        }

        /// <summary>更新攻击射程。</summary>
        internal void SetBaseRange(float value)
        {
            tuning.SetBaseRange(value);
            RefreshView();
        }

        /// <summary>更新一次攻击的投射物数量。</summary>
        internal void SetProjectileCount(float value)
        {
            tuning.SetProjectileCount(value);
            RefreshView();
        }

        /// <summary>更新一次攻击的穿透次数。</summary>
        internal void SetPenetration(float value)
        {
            tuning.SetPenetration(value);
            RefreshView();
        }

        /// <summary>更新蓄力阶段时长。</summary>
        internal void SetChargeDuration(float value)
        {
            tuning.SetChargeDuration(value);
            RefreshView();
        }

        /// <summary>更新缩放脉冲强度。</summary>
        internal void SetScalePunch(float value)
        {
            tuning.SetScalePunch(value);
            ApplyJuiceTuning();
            RefreshView();
        }

        /// <summary>更新缩放脉冲时长。</summary>
        internal void SetPunchDuration(float value)
        {
            tuning.SetPunchDuration(value);
            ApplyJuiceTuning();
            RefreshView();
        }

        /// <summary>更新命中闪烁时长。</summary>
        internal void SetFlashDuration(float value)
        {
            tuning.SetFlashDuration(value);
            ApplyJuiceTuning();
            RefreshView();
        }

        /// <summary>更新命中闪烁颜色。</summary>
        internal void SetFlashColor(Color value)
        {
            tuning.SetFlashColor(value);
            ApplyJuiceTuning();
            RefreshView();
        }

        /// <summary>播放手动目标 Juice。</summary>
        internal void PlayTargetJuice()
        {
            juice?.PlayTargetHit();
            SetStatus("已播放敌人命中 Juice");
        }

        /// <summary>接收真实投射物命中事件并记录结果。</summary>
        private void OnProjectileHit(ProjectileHitContext context)
        {
            if (!ReferenceEquals(context.Target, target))
            {
                return;
            }

            SetStatus(context.DamageResult.WasApplied
                ? $"投射物命中：伤害 {context.DamageResult.AppliedDamage:0.##}"
                : "投射物命中但伤害未生效");
        }

        /// <summary>应用当前会话的 Juice 参数。</summary>
        private void ApplyJuiceTuning()
        {
            juice?.SetTuning(
                tuning.ScalePunch,
                tuning.PunchDuration,
                tuning.FlashColor,
                tuning.FlashDuration);
        }

        /// <summary>创建不写回资产的投射物配置副本。</summary>
        private static ProjectileProfileSO CloneProjectile(ProjectileProfileSO source)
        {
            ProjectileProfileSO clone = ProjectileProfileSO.CreateRuntimeProfile(
                $"CombatPlayground_{source.name}");
            clone.EnergyCost = source.EnergyCost;
            clone.SpeedUnitsPerSecond = source.SpeedUnitsPerSecond;
            clone.MaximumTravelDistance = source.MaximumTravelDistance;
            clone.CollisionRadius = source.CollisionRadius;
            clone.DamageType = source.DamageType;
            clone.TrajectoryDefinition = source.TrajectoryDefinition;
            clone.VisualDefinition = source.VisualDefinition;
            clone.HitEffectId = source.HitEffectId;
            clone.ExpiredEffectId = source.ExpiredEffectId;
            clone.OnHitEffectDefinitions = source.OnHitEffectDefinitions != null
                ? new List<ProjectileEffectDefinitionSO>(source.OnHitEffectDefinitions)
                : null;
            clone.OnExpiredEffectDefinitions = source.OnExpiredEffectDefinitions != null
                ? new List<ProjectileEffectDefinitionSO>(source.OnExpiredEffectDefinitions)
                : null;
            clone.BeforeHitEffectDefinitions = source.BeforeHitEffectDefinitions != null
                ? new List<ProjectileEffectDefinitionSO>(source.BeforeHitEffectDefinitions)
                : null;
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
                target != null && _buffService != null
                    ? _buffService.GetModifierMultiplier(
                        target,
                        BuffModifierType.MovementSpeedMultiplier)
                    : 1f,
                energy,
                _energyService?.MaximumEnergy ?? 0f,
                _hasSelectedTarget,
                _autoFire,
                _shotCount,
                skills?.Length ?? 0,
                _activeProjectile != null ? _activeProjectile.name : "未配置",
                buffDefinition != null ? buffDefinition.name : "未配置",
                _status,
                log);
            _view?.Refresh(in snapshot);
        }
    }
}
