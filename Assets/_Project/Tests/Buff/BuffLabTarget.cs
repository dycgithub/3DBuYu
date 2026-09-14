using CombatSystem;
using EnemySystem;
using Services;
using UnityEngine;
using VContainer;

namespace _Project.Tests.Buff
{
    /// <summary>
    /// CombatLab 中驱动真实 Enemy 完成一次移动 Buff 与伤害验证流程。
    /// </summary>
    public sealed class BuffLabTarget : MonoBehaviour
    {
        [Header("验证目标")]
        [SerializeField] private Enemy _target;

        [Header("Buff 验证配置")]
        [SerializeField] private BuffDefinitionSO _buffDefinition;
        [SerializeField, Min(0f)] private float _damageAmount = 1f;

        private IDamageRequestProcessor _damageProcessor;
        private ICombatPhaseService _combatPhase;
        private DamageResult _damageResult;
        private DamageResult _invalidDamageResult;
        private float _movementMultiplierAfterBuff = 1f;
        private float _movementMultiplierAfterRemoval = 1f;
        private float _damageApplied;
        private bool _invalidTargetRejected;
        private bool _scenarioCompleted;

        /// <summary>验证流程是否已经完成。</summary>
        public bool ScenarioCompleted => _scenarioCompleted;

        /// <summary>施加移动速度 Buff 后读取到的移动速度倍率。</summary>
        public float MovementMultiplierAfterBuff => _movementMultiplierAfterBuff;

        /// <summary>按来源移除 Buff 后读取到的移动速度倍率。</summary>
        public float MovementMultiplierAfterRemoval => _movementMultiplierAfterRemoval;

        /// <summary>移动速度 Buff 存在时实际扣除的伤害。</summary>
        public float DamageApplied => _damageApplied;

        /// <summary>真实敌人目标是否成功处理了伤害。</summary>
        public bool WasDamageApplied => _damageResult.WasApplied;

        /// <summary>无效目标是否被安全拒绝。</summary>
        public bool InvalidTargetRejected => _invalidTargetRejected;

        /// <summary>
        /// 接收 CombatLab 的伤害处理器与阶段服务，避免场景脚本自行创建依赖。
        /// </summary>
        /// <param name="damageProcessor">CombatLab 注册的伤害处理器。</param>
        /// <param name="combatPhase">CombatLab 注册的战斗阶段服务。</param>
        [Inject]
        private void Construct(
            IDamageRequestProcessor damageProcessor,
            ICombatPhaseService combatPhase)
        {
            _damageProcessor = damageProcessor;
            _combatPhase = combatPhase;
        }

        /// <summary>等待真实敌人完成生命初始化后执行一次场景验证。</summary>
        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            RunScenario();
        }

        /// <summary>执行施加、伤害、移除和无效目标四步验证。</summary>
        private void RunScenario()
        {
            if (_scenarioCompleted ||
                _damageProcessor == null ||
                _combatPhase == null ||
                !_combatPhase.CanPerformCombatActions ||
                _buffDefinition == null ||
                _target == null ||
                _target.BuffController == null ||
                _target.MaxHealth <= 0f ||
                _buffDefinition.ModifierType != BuffModifierType.MovementSpeedMultiplier)
            {
                return;
            }

            int sourceInstanceId = GetInstanceID();
            _target.ApplyBuff(_buffDefinition, sourceInstanceId);
            _movementMultiplierAfterBuff = _target.BuffController.GetModifierMultiplier(
                BuffModifierType.MovementSpeedMultiplier);

            DamageRequest request = new()
            {
                SourceInstanceId = sourceInstanceId,
                DamageAmount = _damageAmount,
                DamageType = DamageType.Physical,
                HitPosition = _target.transform.position,
                HitNormal = Vector3.up
            };
            _damageProcessor.TryApplyDamage(_target, in request, out _damageResult);
            _damageApplied = _damageResult.AppliedDamage;

            _target.BuffController.RemoveBySourceId(sourceInstanceId);
            _movementMultiplierAfterRemoval = _target.BuffController.GetModifierMultiplier(
                BuffModifierType.MovementSpeedMultiplier);

            _damageProcessor.TryApplyDamage(null, in request, out _invalidDamageResult);
            _invalidTargetRejected = _invalidDamageResult.Outcome == DamageOutcome.Invalid;
            _scenarioCompleted = true;
        }
    }
}
