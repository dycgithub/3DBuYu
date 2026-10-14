using CombatSystem;
using EnemySystem.BehaviorTree;
using FlockingSystem.ECS;
using Interfaces;
using Services;
using UnityEngine;
using Utils;
using VContainer;
using BehaviorTreeRunner = EnemySystem.BehaviorTree.BehaviorTree;

namespace EnemySystem.Components
{
    /// <summary>以行为树驱动远程 Boss 的距离控制、射击冷却和阶段攻击。</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    [RequireComponent(typeof(EnemyFlockBridge))]
    [RequireComponent(typeof(EnemyCombatAbility))]
    public sealed class EnemyShooterComponent : MonoBehaviour, IPooledObject, IEnemyActivationListener
    {
        private const float MinimumDirectionSquared = 0.0001f;
        private const float MinimumAttackInterval = 0.01f;
        private const float PreferredRangeFraction = 0.75f;

        [Inject] private IPlayerCombatTarget _playerTarget;
        [Inject] private ICombatPhaseService _combatPhase;

        private Enemy _enemy;
        private EnemyFlockBridge _flockBridge;
        private EnemyCombatAbility _ability;
        private BehaviorTreeRunner _tree;
        private EnemyBossPhase _phase;
        private float _cooldown;

        /// <summary>缓存战斗组件并构建远程优先级树。</summary>
        private void Awake()
        {
            CacheComponents();
            BuildTree();
        }

        /// <summary>逐帧推进射击优先级和距离控制节点。</summary>
        private void Update()
        {
            if (!Application.isPlaying || _enemy == null || _enemy.IsDead || _enemy.Attributes == null)
            {
                return;
            }

            UpdatePhase();
            _cooldown = Mathf.Max(0f, _cooldown - Time.deltaTime);
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

        /// <summary>停用时重置树并恢复存活敌人的群游控制。</summary>
        private void OnDisable()
        {
            ResetBehavior();
        }

        /// <summary>对象池租借时清空冷却和阶段状态。</summary>
        public void OnRentFromPool()
        {
            CacheComponents();
            _phase = EnemyBossPhase.First;
            ResetBehavior();
        }

        /// <summary>对象池归还时停止所有当前行为。</summary>
        public void OnReturnToPool()
        {
            ResetBehavior();
            _phase = EnemyBossPhase.First;
        }

        /// <summary>补做注入后的组件引用缓存。</summary>
        void IEnemyActivationListener.OnEnemyActivationReady()
        {
            CacheComponents();
        }

        /// <summary>按优先级处理过近、可射击、接近和群游行为。</summary>
        private void BuildTree()
        {
            _tree = new BehaviorTreeRunner(new SelectorNode(
                new SequenceNode(
                    new ActionNode(NeedsRetreat),
                    new ActionNode(RetreatFromPlayer)),
                new SequenceNode(
                    new ActionNode(() => CheckPhase(EnemyBossPhase.Second)),
                    new ActionNode(CanFire),
                    new ActionNode(() => Fire(EnemyBossPhase.Second))),
                new SequenceNode(
                    new ActionNode(() => CheckPhase(EnemyBossPhase.First)),
                    new ActionNode(CanFire),
                    new ActionNode(() => Fire(EnemyBossPhase.First))),
                new SequenceNode(
                    new ActionNode(NeedsApproach),
                    new ActionNode(ApproachToRange)),
                new ActionNode(ReturnToFlocking)));
        }

        /// <summary>确认当前阶段符合射击分支。</summary>
        private BehaviorStatus CheckPhase(EnemyBossPhase requiredPhase)
        {
            return _phase == requiredPhase
                ? BehaviorStatus.Success
                : BehaviorStatus.Failure;
        }

        /// <summary>检查目标距离是否低于配置的最小射程。</summary>
        private BehaviorStatus NeedsRetreat()
        {
            if (!HasLiveTarget())
            {
                return BehaviorStatus.Failure;
            }

            float distanceSquared = (_playerTarget.Transform.position - transform.position).sqrMagnitude;
            float minimumRange = _enemy.Attributes.shooterMinimumRange;
            return distanceSquared < minimumRange * minimumRange
                ? BehaviorStatus.Success
                : BehaviorStatus.Failure;
        }

        /// <summary>检查攻击限制、战斗阶段、冷却和射程。</summary>
        private BehaviorStatus CanFire()
        {
            if (!HasLiveTarget() ||
                _cooldown > 0f ||
                _enemy.GetAttackAllowedMultiplier() <= 0f ||
                (_combatPhase != null && !_combatPhase.CanPerformCombatActions))
            {
                return BehaviorStatus.Failure;
            }

            float distanceSquared = (_playerTarget.Transform.position - transform.position).sqrMagnitude;
            float maximumRange = _enemy.Attributes.shooterAttackRadius;
            float minimumRange = _enemy.Attributes.shooterMinimumRange;
            return distanceSquared <= maximumRange * maximumRange &&
                   distanceSquared >= minimumRange * minimumRange
                ? BehaviorStatus.Success
                : BehaviorStatus.Failure;
        }

        /// <summary>以当前阶段的冷却和伤害倍率发射玩家目标投射物。</summary>
        private BehaviorStatus Fire(EnemyBossPhase phase)
        {
            if (!HasLiveTarget() || _ability == null)
            {
                return BehaviorStatus.Failure;
            }

            EnemyAttributesSO attributes = _enemy.Attributes;
            Vector3 direction = _playerTarget.Transform.position - transform.position;
            if (direction.sqrMagnitude <= MinimumDirectionSquared)
            {
                return BehaviorStatus.Failure;
            }

            bool accepted = _ability.LaunchProjectile(
                attributes.shooterProjectileProfile,
                attributes.shooterDamage * _enemy.GetAttackDamageMultiplier(),
                direction.normalized,
                1,
                0,
                ProjectileTargetMode.Player);
            if (!accepted)
            {
                return BehaviorStatus.Failure;
            }

            _cooldown = phase == EnemyBossPhase.Second
                ? Mathf.Max(MinimumAttackInterval, attributes.shooterPhaseTwoFireInterval)
                : Mathf.Max(MinimumAttackInterval, attributes.shooterFireInterval);
            return BehaviorStatus.Success;
        }

        /// <summary>检查玩家是否超出期望射击距离。</summary>
        private BehaviorStatus NeedsApproach()
        {
            if (!HasLiveTarget())
            {
                return BehaviorStatus.Failure;
            }

            float desiredRange = _enemy.Attributes.shooterAttackRadius * PreferredRangeFraction;
            return Vector3.Distance(transform.position, _playerTarget.Transform.position) > desiredRange
                ? BehaviorStatus.Success
                : BehaviorStatus.Failure;
        }

        /// <summary>接近至攻击范围内并提交定向移动。</summary>
        private BehaviorStatus ApproachToRange()
        {
            if (!HasLiveTarget())
            {
                return BehaviorStatus.Failure;
            }

            float desiredRange = _enemy.Attributes.shooterAttackRadius * PreferredRangeFraction;
            if (MoveTowards(_playerTarget.Transform.position, desiredRange))
            {
                return BehaviorStatus.Success;
            }

            return BehaviorStatus.Running;
        }

        /// <summary>远离过近玩家直到退出最小射程。</summary>
        private BehaviorStatus RetreatFromPlayer()
        {
            if (!HasLiveTarget())
            {
                return BehaviorStatus.Failure;
            }

            Vector3 towardPlayer = _playerTarget.Transform.position - transform.position;
            float distance = towardPlayer.magnitude;
            float stoppingDistance = _enemy.Attributes.shooterMinimumRange;
            if (distance >= stoppingDistance)
            {
                return BehaviorStatus.Success;
            }

            Vector3 retreatDirection = GetRetreatDirection(towardPlayer, transform.forward);
            Vector3 facingDirection = -retreatDirection;
            float displacement = Mathf.Min(
                GetMoveSpeed() * Time.deltaTime,
                stoppingDistance - distance);
            Quaternion rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(facingDirection, Vector3.up),
                GetTurnSpeed() * Time.deltaTime);

            _flockBridge.SetMotionMode(EnemyFlockMotionMode.Directed);
            _flockBridge.SetDirectedMotion(retreatDirection * displacement, rotation);
            return BehaviorStatus.Running;
        }

        /// <summary>在目标方向退化时沿敌人后方选择确定的撤退方向。</summary>
        /// <param name="towardPlayer">指向玩家的方向。</param>
        /// <param name="fallbackForward">敌人当前前向。</param>
        /// <returns>远离玩家的单位方向。</returns>
        private static Vector3 GetRetreatDirection(Vector3 towardPlayer, Vector3 fallbackForward)
        {
            return towardPlayer.sqrMagnitude > MinimumDirectionSquared
                ? -towardPlayer.normalized
                : -fallbackForward.normalized;
        }

        /// <summary>移动到目标停止距离并保留目标朝向。</summary>
        private bool MoveTowards(Vector3 targetPosition, float stoppingDistance)
        {
            Vector3 offset = targetPosition - transform.position;
            float distance = offset.magnitude;
            if (distance <= stoppingDistance)
            {
                _flockBridge.SetMotionMode(EnemyFlockMotionMode.Directed);
                _flockBridge.SetDirectedMotion(Vector3.zero, transform.rotation);
                return true;
            }

            if (offset.sqrMagnitude <= MinimumDirectionSquared)
            {
                return false;
            }

            Quaternion rotation = Quaternion.RotateTowards(
                transform.rotation,
                Quaternion.LookRotation(offset, Vector3.up),
                GetTurnSpeed() * Time.deltaTime);
            float distanceThisFrame = Mathf.Min(
                GetMoveSpeed() * Time.deltaTime,
                distance - stoppingDistance);

            _flockBridge.SetMotionMode(EnemyFlockMotionMode.Directed);
            _flockBridge.SetDirectedMotion(offset.normalized * distanceThisFrame, rotation);
            return false;
        }

        /// <summary>没有有效目标时恢复群游。</summary>
        private BehaviorStatus ReturnToFlocking()
        {
            _flockBridge.SetMotionMode(EnemyFlockMotionMode.Flocking);
            return BehaviorStatus.Success;
        }

        /// <summary>只允许 Boss 从第一阶段进入第二阶段。</summary>
        private void UpdatePhase()
        {
            if (_phase == EnemyBossPhase.First &&
                _enemy.HealthPercent <= _enemy.Attributes.bossSecondPhaseHealthPercent)
            {
                _phase = EnemyBossPhase.Second;
                _tree.Reset();
                _cooldown = 0f;
            }
        }

        /// <summary>检查玩家目标当前是否有效。</summary>
        private bool HasLiveTarget()
        {
            return _playerTarget != null &&
                   _playerTarget.IsAlive &&
                   _playerTarget.Transform != null;
        }

        /// <summary>读取敌人群游配置中的最大速度和阶段倍率。</summary>
        private float GetMoveSpeed()
        {
            float baseSpeed = _enemy.FlockSettings != null
                ? _enemy.FlockSettings.GetProfileForEnemyType(
                    _enemy.EnemyType,
                    _enemy.SpeedMultiplier).MaxSpeed
                : 0f;
            float phaseMultiplier = _phase == EnemyBossPhase.Second
                ? _enemy.Attributes.bossPhaseTwoSpeedMultiplier
                : 1f;
            return baseSpeed * _enemy.GetMovementSpeedMultiplier() * phaseMultiplier;
        }

        /// <summary>读取敌人群游配置中的最大转向速度。</summary>
        private float GetTurnSpeed()
        {
            return _enemy.FlockSettings != null
                ? _enemy.FlockSettings.GetProfileForEnemyType(
                    _enemy.EnemyType,
                    _enemy.SpeedMultiplier).RotationSpeed
                : 0f;
        }

        /// <summary>清空树状态、冷却和运动所有权。</summary>
        private void ResetBehavior()
        {
            _tree?.Reset();
            _cooldown = 0f;
            if (gameObject.activeInHierarchy &&
                _flockBridge != null &&
                (_enemy == null || !_enemy.IsDead))
            {
                _flockBridge.SetMotionMode(EnemyFlockMotionMode.Flocking);
            }
        }

        /// <summary>缓存实体、运动桥接和通用攻击组件。</summary>
        private void CacheComponents()
        {
            _enemy ??= GetComponent<Enemy>();
            _flockBridge ??= GetComponent<EnemyFlockBridge>();
            _ability ??= GetComponent<EnemyCombatAbility>();
        }
    }
}
