using System.Collections.Generic;
using CombatSystem;
using EnemySystem.AreaEffect.Implement;
using EnemySystem.AreaEffect;
using EnemySystem.AreaEffect.Model;
using FlockingSystem.ECS;
using Interfaces;
using Services;
using SpatialIndexing;
using UnityEngine;
using Utils;
using VContainer;

namespace EnemySystem.Components
{
    /// <summary>
    /// 推进范围精英战术状态，并在 Casting 状态把范围 Buff 交给集中服务处理。
    /// <para>目标搜索按感知间隔节流；运动命令由 ECS Flocking 桥接提交。</para>
    /// <para>范围只决定可以新增哪些目标；已施加的 Buff 按自身持续时间独立结束。</para>
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    public sealed class EnemyAreaEffectComponent : MonoBehaviour, IPooledObject, IEnemyActivationListener
    {
        private const float MaximumAreaEliteSpeed = 2.5f;

        [Inject] private ISpatialQueryService _spatialService;
        [Inject] private IPlayerCombatTarget _playerTarget;
        [Inject] private EnemyAreaEffectService _areaEffectService;

        private readonly List<IDamageable> _candidateTargets = new(EnemyAreaEffectScheduler.MaximumTargets);
        private readonly List<AffectedTarget> _affectedTargets = new(EnemyAreaEffectScheduler.MaximumTargets);

        private Enemy _enemy;
        private EnemyFlockBridge _flockBridge;
        private EnemyAreaEffectStateMachine _stateMachine;
        private EnemyAttributesSO _stateMachineAttributes;
        private readonly EnemyAreaEffectTargeting _targeting = new();
        private BuffDefinitionSO _runtimeSlowBuff;
        private BuffDefinitionSO _effectBuff;
        private EnemyAreaEffectType _effectType;
        private float _effectRadius;
        private float _effectRadiusSquared;
        private float _effectInterval;
        private float _elapsedSeconds;
        private bool _configurationCached;
        private bool _registered;
        private readonly EnemyAreaEffectScheduler _scheduler = new();

        private struct AffectedTarget
        {
            public IDamageable Target;
            public float ExpireAt;
        }

        /// <summary>缓存宿主敌人并清空范围状态。</summary>
        private void Awake()
        {
            CacheComponents();
            InitializeStateMachine();
            ResetState();
        }

        /// <summary>启用时重置周期状态，并尝试登记到集中调度器。</summary>
        private void OnEnable()
        {
            CacheComponents();
            InitializeStateMachine();
            ResetState();
            SubscribeToDeath();
            TryRegister();
        }

        /// <summary>禁用时注销调度器并清理本次租借的候选状态。</summary>
        private void OnDisable()
        {
            UnsubscribeFromDeath();
            ResetState();
        }

        /// <summary>释放本组件创建的运行时减速定义。</summary>
        private void OnDestroy()
        {
            UnsubscribeFromDeath();
            Unregister();
            if (_runtimeSlowBuff == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_runtimeSlowBuff);
            }
            else
            {
                DestroyImmediate(_runtimeSlowBuff);
            }

            _runtimeSlowBuff = null;
        }

        /// <summary>逐帧感知目标、推进状态机并提交 ECS 定向运动意图。</summary>
        private void Update()
        {
            if (!Application.isPlaying || _enemy == null || _enemy.IsDead || _enemy.Attributes == null)
            {
                return;
            }

            InitializeStateMachine();
            if (_stateMachine == null)
            {
                return;
            }

            float deltaTime = Mathf.Max(0f, Time.deltaTime);
            _targeting.Tick(
                _enemy,
                _spatialService,
                _playerTarget,
                _stateMachine.CurrentState,
                deltaTime);
            _stateMachine.Tick(_targeting.CreateFrame(_enemy, _playerTarget, deltaTime));
            ApplyMovement(_stateMachine.CurrentIntent, deltaTime);
        }

        /// <summary>在 Casting 状态推进范围效果周期。</summary>
        /// <param name="deltaTime">自上次处理以来经过的秒数。</param>
        internal void ProcessFrame(float deltaTime)
        {
            if (_enemy == null)
            {
                TryGetComponent(out _enemy);
            }

            if (!isActiveAndEnabled || _enemy == null || _enemy.IsDead ||
                _stateMachine == null || !_stateMachine.IsCasting)
            {
                return;
            }

            _elapsedSeconds += Mathf.Max(0f, deltaTime);
            CacheConfiguration();
            if (_effectBuff == null)
            {
                return;
            }

            PruneAffectedTargets();
            if (!_scheduler.TryBeginTick(_elapsedSeconds, _effectInterval))
            {
                return;
            }

            RefreshCandidates();
            TryApplyNextTarget();
        }

        /// <summary>对象池租借时重置候选窗口和范围状态。</summary>
        public void OnRentFromPool() => ResetState();

        /// <summary>对象池归还时重置候选窗口和范围状态。</summary>
        public void OnReturnToPool() => ResetState();

        /// <summary>补做宿主激活后的依赖缓存和调度登记。</summary>
        void IEnemyActivationListener.OnEnemyActivationReady()
        {
            CacheComponents();
            InitializeStateMachine();
            TryRegister();
        }

        /// <summary>死亡时重置战术状态，并保留视觉溶解所需的 ECS 冻结。</summary>
        private void HandleDeathStarted(Enemy enemy) => ResetState(EnemyFlockMotionMode.Frozen);

        /// <summary>订阅宿主死亡事件，以便立即停止该范围源。</summary>
        private void SubscribeToDeath()
        {
            if (_enemy != null)
            {
                _enemy.DeathStarted -= HandleDeathStarted;
                _enemy.DeathStarted += HandleDeathStarted;
            }
        }

        /// <summary>解除宿主死亡事件订阅。</summary>
        private void UnsubscribeFromDeath()
        {
            if (_enemy != null)
            {
                _enemy.DeathStarted -= HandleDeathStarted;
            }
        }

        /// <summary>按当前敌人配置创建或复用状态机。</summary>
        private void InitializeStateMachine()
        {
            EnemyAttributesSO attributes = _enemy != null ? _enemy.Attributes : null;
            if (attributes == null || attributes == _stateMachineAttributes)
            {
                return;
            }

            _stateMachineAttributes = attributes;
            _stateMachine = new EnemyAreaEffectStateMachine(attributes);
        }

        /// <summary>把状态机意图翻译为 ECS 的群游、定向或保持命令。</summary>
        private void ApplyMovement(EnemyAreaEffectIntent intent, float deltaTime)
        {
            switch (intent)
            {
                case EnemyAreaEffectIntent.Flocking:
                    _flockBridge?.SetMotionMode(EnemyFlockMotionMode.Flocking);
                    break;
                case EnemyAreaEffectIntent.Approach:
                    MoveTowardsTarget(deltaTime);
                    break;
                case EnemyAreaEffectIntent.Hold:
                    SetDirectedMotion(Vector3.zero, transform.rotation);
                    break;
                case EnemyAreaEffectIntent.Retreat:
                    MoveAwayFromPlayer(deltaTime);
                    break;
            }
        }

        /// <summary>向施法目标低速移动至范围效果半径。</summary>
        private void MoveTowardsTarget(float deltaTime)
        {
            IDamageable target = _targeting.Target;
            if (_flockBridge == null || target == null)
            {
                _flockBridge?.SetMotionMode(EnemyFlockMotionMode.Flocking);
                return;
            }

            EnemyAreaEffectMotion motion = EnemyAreaEffectMotionMath.Approach(
                transform.position,
                transform.rotation,
                target.Position,
                _enemy.Attributes.areaEffectRadius,
                GetMoveSpeed(_enemy.Attributes.areaApproachSpeed),
                _enemy.Attributes.areaTurnSpeed,
                deltaTime);
            SetDirectedMotion(motion.Displacement, motion.Rotation);
        }

        /// <summary>按生命对应的安全距离沿玩家反方向移动。</summary>
        private void MoveAwayFromPlayer(float deltaTime)
        {
            if (_flockBridge == null || _playerTarget == null ||
                !_playerTarget.IsAlive || _playerTarget.Transform == null)
            {
                _flockBridge?.SetMotionMode(EnemyFlockMotionMode.Flocking);
                return;
            }

            float safeDistance = EnemyAreaEffectTactics.RetreatDistance(
                _enemy.Attributes.areaPreferredDistance,
                _enemy.Attributes.areaMaximumRetreatDistance,
                _enemy.HealthPercent);
            EnemyAreaEffectMotion motion = EnemyAreaEffectMotionMath.Retreat(
                transform.position,
                transform.rotation,
                _playerTarget.Position,
                safeDistance,
                GetMoveSpeed(_enemy.Attributes.areaRetreatSpeed),
                _enemy.Attributes.areaTurnSpeed,
                deltaTime);
            SetDirectedMotion(motion.Displacement, motion.Rotation);
        }

        /// <summary>应用难度与 Buff 倍率后限制范围精英速度。</summary>
        private float GetMoveSpeed(float configuredSpeed)
        {
            return EnemyAreaEffectTactics.ClampSpeed(
                configuredSpeed,
                _enemy.GetMovementSpeedMultiplier(),
                MaximumAreaEliteSpeed);
        }

        /// <summary>由组件持有 ECS 定向运动命令的提交权。</summary>
        private void SetDirectedMotion(Vector3 displacement, Quaternion rotation)
        {
            if (_flockBridge == null)
            {
                return;
            }

            _flockBridge.SetMotionMode(EnemyFlockMotionMode.Directed);
            _flockBridge.SetDirectedMotion(displacement, rotation);
        }

        /// <summary>登记当前范围源，兼容注入先后顺序和对象池重复激活。</summary>
        private void TryRegister()
        {
            CacheComponents();
            InitializeStateMachine();

            if (!isActiveAndEnabled || _areaEffectService == null || _enemy == null ||
                _enemy.IsDead || _enemy.Attributes == null)
            {
                return;
            }

            CacheConfiguration();
            if (!_registered)
            {
                _areaEffectService.Register(this);
                _registered = true;
            }
        }

        /// <summary>从集中调度器注销当前范围源。</summary>
        private void Unregister()
        {
            if (!_registered)
            {
                return;
            }

            _areaEffectService?.Unregister(this);
            _registered = false;
        }

        /// <summary>读取一次本次租借的范围参数和对应 Buff。</summary>
        private void CacheConfiguration()
        {
            if (_configurationCached || _enemy == null || _enemy.Attributes == null)
            {
                return;
            }

            EnemyAttributesSO attributes = _enemy.Attributes;
            InitializeStateMachine();
            _effectType = attributes.areaEffectType;
            _effectRadius = Mathf.Max(0f, attributes.areaEffectRadius);
            _effectRadiusSquared = _effectRadius * _effectRadius;
            _effectInterval = attributes.areaEffectTickInterval;
            _effectBuff = ResolveEffectBuff(attributes);
            _configurationCached = true;
        }

        /// <summary>按范围模式返回减速或持续治疗 Buff。</summary>
        private BuffDefinitionSO ResolveEffectBuff(EnemyAttributesSO attributes)
        {
            if (_effectType != EnemyAreaEffectType.Slow)
            {
                return attributes.areaHealBuff;
            }

            _runtimeSlowBuff = EnemyAreaEffectBuffFactory.GetSlowBuff(
                attributes,
                _runtimeSlowBuff,
                name);
            return _runtimeSlowBuff;
        }

        /// <summary>刷新当前范围内的轮转候选窗口，结果写入复用缓存。</summary>
        private void RefreshCandidates()
        {
            if (_spatialService == null)
            {
                _candidateTargets.Clear();
                return;
            }

            List<IDamageable> targets = _spatialService.QueryRadius(
                transform.position,
                _effectRadius,
                SpatialLayers.Enemy);
            _scheduler.CopyCandidateWindow(targets, _candidateTargets);
        }

        /// <summary>每个周期最多成功施加一次；HealAll 优先敌人，未成功时回退到玩家。</summary>
        private void TryApplyNextTarget()
        {
            switch (_effectType)
            {
                case EnemyAreaEffectType.Slow:
                    TryApplyToPlayer();
                    break;
                case EnemyAreaEffectType.HealAll:
                    if (_affectedTargets.Count >= EnemyAreaEffectScheduler.MaximumTargets ||
                        !TryApplyToEnemy())
                    {
                        TryApplyToPlayer();
                    }
                    break;
                case EnemyAreaEffectType.HealEnemies:
                    if (_affectedTargets.Count < EnemyAreaEffectScheduler.MaximumTargets)
                    {
                        TryApplyToEnemy();
                    }
                    break;
            }
        }

        /// <summary>按轮转后的窗口顺序跳过无效目标，直到成功施加一次。</summary>
        private bool TryApplyToEnemy()
        {
            int count = _candidateTargets.Count;
            if (count == 0)
            {
                return false;
            }

            for (int index = 0; index < count; index++)
            {
                IDamageable target = _candidateTargets[index];
                if (!IsTargetInRange(target) || target is not IBuffReceiver receiver)
                {
                    continue;
                }

                if (!receiver.TryApplyBuffIfAbsent(_effectBuff, GetInstanceID()))
                {
                    continue;
                }

                RecordAffectedTarget(target);
                return true;
            }

            return false;
        }

        /// <summary>按当前模式尝试给范围内玩家施加一次 Buff。</summary>
        private bool TryApplyToPlayer() =>
            _effectType != EnemyAreaEffectType.HealEnemies &&
            IsTargetInRange(_playerTarget) &&
            _playerTarget is IBuffReceiver receiver &&
            receiver.TryApplyBuffIfAbsent(_effectBuff, GetInstanceID());

        /// <summary>记录一个本来源仍占用的受影响目标槽位。</summary>
        private void RecordAffectedTarget(IDamageable target)
        {
            float expireAt = GetExpireAt();
            for (int index = 0; index < _affectedTargets.Count; index++)
            {
                if (_affectedTargets[index].Target != target)
                {
                    continue;
                }

                _affectedTargets[index] = new AffectedTarget
                {
                    Target = target,
                    ExpireAt = expireAt
                };
                return;
            }

            _affectedTargets.Add(new AffectedTarget
            {
                Target = target,
                ExpireAt = expireAt
            });
        }

        /// <summary>移除死亡、销毁或已自然过期的受影响目标。</summary>
        private void PruneAffectedTargets()
        {
            float currentTime = Time.time;
            for (int index = _affectedTargets.Count - 1; index >= 0; index--)
            {
                AffectedTarget affected = _affectedTargets[index];
                if (!IsAlive(affected.Target) ||
                    (affected.ExpireAt < float.PositiveInfinity && currentTime >= affected.ExpireAt))
                {
                    _affectedTargets.RemoveAt(index);
                }
            }
        }

        /// <summary>判断目标存活且仍在当前范围内。</summary>
        private bool IsTargetInRange(IDamageable target) => IsAlive(target) &&
            (target.Position - transform.position).sqrMagnitude <= _effectRadiusSquared;

        /// <summary>计算本次受影响目标槽位的自然过期时间。</summary>
        private float GetExpireAt() => _effectBuff?.DurationSeconds > 0f
            ? Time.time + _effectBuff.DurationSeconds : float.PositiveInfinity;
        /// <summary>缓存敌人宿主和 ECS 运动桥接。</summary>
        private void CacheComponents() =>
            (_enemy, _flockBridge) = (_enemy ?? GetComponent<Enemy>(), _flockBridge ?? GetComponent<EnemyFlockBridge>());

        /// <summary>判断目标接口引用仍对应有效的 Unity 对象。</summary>
        private static bool IsAlive(IDamageable target) => target != null &&
            (!(target is Object unityObject) || unityObject != null) &&
            target.IsAlive;

        /// <summary>清除本次租借的候选、受影响目标和调度状态。</summary>
        private void ResetState(EnemyFlockMotionMode motionMode = EnemyFlockMotionMode.Flocking)
        {
            Unregister();
            _candidateTargets.Clear();
            _affectedTargets.Clear();
            _targeting.Reset();
            _elapsedSeconds = 0f;
            _scheduler.Reset();
            _configurationCached = false;
            _effectBuff = null;
            _stateMachine?.Reset();
            _flockBridge?.SetMotionMode(motionMode);
        }
    }
}
