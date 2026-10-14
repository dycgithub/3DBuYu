using EnemySystem.BehaviorTree;
using FlockingSystem.ECS;
using GeometricMotion;
using Interfaces;
using Services;
using UnityEngine;
using Utils;
using VContainer;
using BehaviorTreeRunner = EnemySystem.BehaviorTree.BehaviorTree;

namespace EnemySystem.Components
{
    /// <summary>以行为树驱动冲锋 Boss 的转向、蓄力、冲刺与阶段攻击。</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    [RequireComponent(typeof(EnemyFlockBridge))]
    public sealed class EnemyAssaultComponent : MonoBehaviour, IPooledObject, IEnemyActivationListener
    {
        private const float MinimumDirectionSquared = 0.0001f;
        private const float MinimumAttackCooldown = 0.01f;

        [Inject] private IPlayerCombatTarget _playerTarget;
        [Inject] private ICombatPhaseService _combatPhase;

        private Enemy _enemy;
        private EnemyFlockBridge _flockBridge;
        private BehaviorTreeRunner _tree;
        private EnemyAssaultState _state;
        private EnemyBossPhase _phase;
        private float _attackCooldown;
        private float _stateTimer;
        private float _dashDistance;
        private float _recoveryTimer;
        private bool _isRecovering;
        private Vector3 _dashDirection;
        private Quaternion _nextTurnRotation;

        /// <summary>当前 BT 动作对应的冲锋阶段，供 Inspector 调试。</summary>
        public EnemyAssaultState CurrentState => _state;

        /// <summary>缓存实体并为该实例构建独立行为树。</summary>
        private void Awake()
        {
            CacheComponents();
            BuildTree();
        }

        /// <summary>验证目标后逐帧推进行为树。</summary>
        private void Update()
        {
            if (!Application.isPlaying || _enemy == null || _enemy.IsDead || _enemy.Attributes == null)
            {
                return;
            }

            UpdatePhase();
            _attackCooldown = Mathf.Max(0f, _attackCooldown - Time.deltaTime);
            _tree.Tick();
        }

        /// <summary>确保 Inspector 中实例也能构建树，但只在运行时推进动作。</summary>
        private void OnEnable()
        {
            CacheComponents();
            if (_tree == null)
            {
                BuildTree();
            }
        }

        /// <summary>停用时中止当前行为并恢复存活敌人的群游。</summary>
        private void OnDisable()
        {
            ResetBehavior();
        }

        /// <summary>对象池租借时清除行为树状态和阶段。</summary>
        public void OnRentFromPool()
        {
            CacheComponents();
            _phase = EnemyBossPhase.First;
            ResetBehavior();
        }

        /// <summary>对象池归还时取消动作并清空所有计时数据。</summary>
        public void OnReturnToPool()
        {
            ResetBehavior();
            _phase = EnemyBossPhase.First;
        }

        /// <summary>注入完成后补齐组件缓存。</summary>
        void IEnemyActivationListener.OnEnemyActivationReady()
        {
            CacheComponents();
        }

        /// <summary>按 Boss 阶段构建优先级冲锋树。</summary>
        private void BuildTree()
        {
            _tree = new BehaviorTreeRunner(new SelectorNode(
                BuildAttackBranch(EnemyBossPhase.Second),
                BuildAttackBranch(EnemyBossPhase.First),
                new ActionNode(ApproachPlayer),
                new ActionNode(ReturnToFlocking)));
        }

        /// <summary>创建单段或多段冲锋分支。</summary>
        private BehaviorNode BuildAttackBranch(EnemyBossPhase phase)
        {
            BehaviorNode cycle = new SequenceNode(
                new ActionNode(TurnToPlayer),
                new ActionNode(BeginCharge),
                new ActionNode(Charge),
                new ActionNode(BeginDash),
                new ActionNode(Dash),
                new ActionNode(Recover, ResetRecovery));
            return new SequenceNode(
                new ActionNode(() => CheckPhase(phase)),
                new ActionNode(CanBeginAttack),
                new ActionNode(BeginAttack),
                new RepeatNode(cycle, () => GetDashCount(phase)));
        }

        /// <summary>确认当前阶段与分支要求相同。</summary>
        private BehaviorStatus CheckPhase(EnemyBossPhase requiredPhase)
        {
            return _phase == requiredPhase
                ? BehaviorStatus.Success
                : BehaviorStatus.Failure;
        }

        /// <summary>检查玩家、攻击限制、战斗阶段和感知范围。</summary>
        private BehaviorStatus CanBeginAttack()
        {
            if (!HasLiveTarget() ||
                _attackCooldown > 0f ||
                _enemy.GetAttackAllowedMultiplier() <= 0f ||
                (_combatPhase != null && !_combatPhase.CanPerformCombatActions) ||
                !IsWithinPerceptionRadius())
            {
                return BehaviorStatus.Failure;
            }

            return BehaviorStatus.Success;
        }

        /// <summary>启动一轮冲锋并开始攻击冷却。</summary>
        private BehaviorStatus BeginAttack()
        {
            EnemyAttributesSO attributes = _enemy.Attributes;
            _attackCooldown = Mathf.Max(
                MinimumAttackCooldown,
                attributes.assaultChargeDuration + attributes.assaultAttackRecoveryDuration);
            return BehaviorStatus.Success;
        }

        /// <summary>平滑转向玩家，达到配置角度后结束动作。</summary>
        private BehaviorStatus TurnToPlayer()
        {
            if (!HasTargetDirection())
            {
                return BehaviorStatus.Failure;
            }

            _state = EnemyAssaultState.Turning;
            _flockBridge.SetMotionMode(EnemyFlockMotionMode.Directed);
            Quaternion target = Quaternion.LookRotation(GetTargetDirection(), Vector3.up);
            _nextTurnRotation = Quaternion.RotateTowards(
                transform.rotation,
                target,
                _enemy.Attributes.assaultTurnSpeed * Time.deltaTime);
            _flockBridge.SetDirectedMotion(Vector3.zero, _nextTurnRotation);
            return Quaternion.Angle(_nextTurnRotation, target) <=
                   _enemy.Attributes.assaultTurnTolerance
                ? BehaviorStatus.Success
                : BehaviorStatus.Running;
        }

        /// <summary>发出蓄力信号并重置蓄力计时。</summary>
        private BehaviorStatus BeginCharge()
        {
            _state = EnemyAssaultState.Charging;
            _stateTimer = 0f;
            _flockBridge.SignalMotion(GeometricMotionSignalType.Prepare);
            return BehaviorStatus.Success;
        }

        /// <summary>蓄力期间持续验证目标并推进计时。</summary>
        private BehaviorStatus Charge()
        {
            if (!HasLiveTarget() || !HasTargetDirection())
            {
                return BehaviorStatus.Failure;
            }

            _stateTimer += Time.deltaTime;
            return _stateTimer >= _enemy.Attributes.assaultChargeDuration
                ? BehaviorStatus.Success
                : BehaviorStatus.Running;
        }

        /// <summary>锁定冲刺方向并发送释放表现信号。</summary>
        private BehaviorStatus BeginDash()
        {
            if (!HasTargetDirection())
            {
                return BehaviorStatus.Failure;
            }

            _state = EnemyAssaultState.Dashing;
            _dashDirection = GetTargetDirection().normalized;
            _dashDistance = 0f;
            _flockBridge.SignalMotion(GeometricMotionSignalType.Release);
            return BehaviorStatus.Success;
        }

        /// <summary>通过 ECS 提交本帧冲刺位移。</summary>
        private BehaviorStatus Dash()
        {
            if (_enemy.GetMovementSpeedMultiplier() <= 0f ||
                _enemy.Attributes.assaultDashSpeed <= 0f)
            {
                return BehaviorStatus.Failure;
            }

            float speed = GetDashSpeed();
            float remaining = Mathf.Max(0f, _enemy.Attributes.assaultDashDistance - _dashDistance);
            float distance = Mathf.Min(speed * Time.deltaTime, remaining);
            if (distance > 0f)
            {
                _flockBridge.SetDirectedMotion(
                    _dashDirection * distance,
                    transform.rotation);
                _dashDistance += distance;
            }

            return _dashDistance >= _enemy.Attributes.assaultDashDistance
                ? BehaviorStatus.Success
                : BehaviorStatus.Running;
        }

        /// <summary>恢复一段时间后允许重复冲锋。</summary>
        private BehaviorStatus Recover()
        {
            if (!_isRecovering)
            {
                _isRecovering = true;
                _recoveryTimer = 0f;
            }

            _recoveryTimer += Time.deltaTime;
            if (_recoveryTimer < _enemy.Attributes.assaultAttackRecoveryDuration)
            {
                return BehaviorStatus.Running;
            }

            _isRecovering = false;
            return BehaviorStatus.Success;
        }

        /// <summary>当玩家超出感知范围时接近玩家。</summary>
        private BehaviorStatus ApproachPlayer()
        {
            if (!HasLiveTarget())
            {
                _state = EnemyAssaultState.Roaming;
                return BehaviorStatus.Failure;
            }

            if (IsWithinPerceptionRadius())
            {
                _state = EnemyAssaultState.Roaming;
                ResumeFlocking();
                return BehaviorStatus.Success;
            }

            _state = EnemyAssaultState.Roaming;
            MoveToward(_playerTarget.Transform.position, _enemy.Attributes.assaultPerceptionRadius);
            return BehaviorStatus.Running;
        }

        /// <summary>无玩家目标时恢复群游控制。</summary>
        private BehaviorStatus ReturnToFlocking()
        {
            _state = EnemyAssaultState.Roaming;
            ResumeFlocking();
            return BehaviorStatus.Success;
        }

        /// <summary>读取阶段对应的冲锋次数。</summary>
        private int GetDashCount(EnemyBossPhase phase)
        {
            return phase == EnemyBossPhase.Second
                ? Mathf.Max(1, _enemy.Attributes.assaultPhaseTwoDashCount)
                : 1;
        }

        /// <summary>沿目标方向提交一次定向移动。</summary>
        private void MoveToward(Vector3 targetPosition, float stoppingDistance)
        {
            Vector3 offset = targetPosition - transform.position;
            if (offset.sqrMagnitude <= MinimumDirectionSquared)
            {
                return;
            }

            float speed = GetBaseSpeed() *
                          _enemy.GetMovementSpeedMultiplier() *
                          GetPhaseSpeedMultiplier();
            float distance = Mathf.Min(
                speed * Time.deltaTime,
                Mathf.Max(0f, offset.magnitude - stoppingDistance));
            Quaternion rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(offset, Vector3.up),
                _enemy.Attributes.assaultTurnSpeed * Time.deltaTime);
            _flockBridge.SetMotionMode(EnemyFlockMotionMode.Directed);
            _flockBridge.SetDirectedMotion(offset.normalized * distance, rotation);
        }

        /// <summary>清理冲锋恢复状态。</summary>
        private void ResetRecovery()
        {
            _isRecovering = false;
            _recoveryTimer = 0f;
        }

        /// <summary>检查玩家目标当前是否可用于进攻。</summary>
        private bool HasLiveTarget()
        {
            return _playerTarget != null &&
                   _playerTarget.IsAlive &&
                   _playerTarget.Transform != null;
        }

        /// <summary>计算敌人指向玩家的世界方向。</summary>
        private Vector3 GetTargetDirection()
        {
            return _playerTarget.Transform.position - transform.position;
        }

        /// <summary>检查玩家是否位于配置的感知范围。</summary>
        private bool IsWithinPerceptionRadius()
        {
            return HasLiveTarget() &&
                   (transform.position - _playerTarget.Transform.position).sqrMagnitude <=
                   _enemy.Attributes.assaultPerceptionRadius * _enemy.Attributes.assaultPerceptionRadius;
        }

        /// <summary>检查玩家方向是否可用于转向或冲刺。</summary>
        private bool HasTargetDirection()
        {
            return HasLiveTarget() && GetTargetDirection().sqrMagnitude > MinimumDirectionSquared;
        }

        /// <summary>只允许 Boss 从第一阶段向第二阶段切换。</summary>
        private void UpdatePhase()
        {
            if (_phase == EnemyBossPhase.First &&
                _enemy.HealthPercent <= _enemy.Attributes.bossSecondPhaseHealthPercent)
            {
                _phase = EnemyBossPhase.Second;
                ResetBehavior();
                _attackCooldown = 0f;
            }
        }

        /// <summary>读取群游配置中的速度。</summary>
        private float GetBaseSpeed()
        {
            return _enemy.FlockSettings != null
                ? _enemy.FlockSettings.GetProfileForEnemyType(
                    _enemy.EnemyType,
                    _enemy.SpeedMultiplier).MaxSpeed
                : 0f;
        }

        /// <summary>读取配置基础冲刺速度及当前阶段倍率。</summary>
        private float GetDashSpeed()
        {
            return _enemy.Attributes.assaultDashSpeed *
                   _enemy.GetMovementSpeedMultiplier() *
                   GetPhaseSpeedMultiplier();
        }

        /// <summary>取得第二阶段移动速度倍率。</summary>
        private float GetPhaseSpeedMultiplier()
        {
            return _phase == EnemyBossPhase.Second
                ? _enemy.Attributes.bossPhaseTwoSpeedMultiplier
                : 1f;
        }

        /// <summary>中止树内动作并清空冲刺计时器。</summary>
        private void ResetBehavior()
        {
            _tree?.Reset();
            _attackCooldown = 0f;
            _stateTimer = 0f;
            _dashDistance = 0f;
            _dashDirection = Vector3.zero;
            ResetRecovery();
            if (gameObject.activeInHierarchy &&
                _flockBridge != null &&
                (_enemy == null || !_enemy.IsDead))
            {
                ResumeFlocking();
            }
        }

        /// <summary>恢复 ECS 群游运动所有权。</summary>
        private void ResumeFlocking()
        {
            _flockBridge.SetMotionMode(EnemyFlockMotionMode.Flocking);
        }

        /// <summary>缓存实体和 ECS 移动桥接。</summary>
        private void CacheComponents()
        {
            _enemy ??= GetComponent<Enemy>();
            _flockBridge ??= GetComponent<EnemyFlockBridge>();
        }
    }
}
