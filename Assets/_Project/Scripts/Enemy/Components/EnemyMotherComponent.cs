using System;
using System.Collections.Generic;
using EnemySystem.GOAP;
using FlockingSystem.ECS;
using Interfaces;
using Services;
using UnityEngine;
using Utils;
using VContainer;

namespace EnemySystem.Components
{
    /// <summary>用 GOAP 平衡子代数量与接近玩家的母体 Boss。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    [RequireComponent(typeof(EnemyFlockBridge))]
    public sealed class EnemyMotherComponent : MonoBehaviour, IPooledObject, IEnemyActivationListener
    {
        private const float MinimumPlanSpeed = 0.01f;
        private const float MinimumSpawnInterval = 0.01f;

        [Inject] private IEnemyChildSpawner _childSpawner;
        [Inject] private IPlayerCombatTarget _playerTarget;

        private readonly HashSet<Enemy> _children = new();
        private readonly Dictionary<Enemy, Action> _childReuseHandlers = new();
        private readonly List<Enemy> _iterationBuffer = new(8);
        private readonly List<GoapActionDefinition> _actions = new(1);
        private readonly List<GoapActionDefinition> _plan = new(1);

        private Enemy _enemy;
        private EnemyFlockBridge _flockBridge;
        private float _spawnTimer;
        private float _decisionTimer;
        private EnemyBossPhase _phase;
        private GoapActionId _activeAction;
        private bool _parentDeathSubscribed;

        /// <summary>当前母体生成且仍存活的子敌人数。</summary>
        public int ActiveChildCount => _children.Count;

        /// <summary>缓存宿主、移动桥接并监听母体死亡。</summary>
        private void Awake()
        {
            CacheComponents();
        }

        /// <summary>重置本轮 GOAP 状态并恢复父代死亡订阅。</summary>
        private void OnEnable()
        {
            CacheComponents();
            ResetPlan();
            _decisionTimer = 0f;
            SubscribeParentDeath();
        }

        /// <summary>暂时停用时暂停 GOAP，但保留父子死亡关系。</summary>
        private void OnDisable()
        {
            ResetPlan();
            if (gameObject.activeInHierarchy && (_enemy == null || !_enemy.IsDead))
            {
                EnemyGoapMotion.ResumeFlocking(_flockBridge);
            }
        }

        /// <summary>销毁时清理父代和子代事件订阅。</summary>
        private void OnDestroy()
        {
            UnsubscribeParentDeath();
            ClearChildren();
        }

        /// <summary>按目标子代数量规划召唤，其他时间接近玩家施压。</summary>
        private void Update()
        {
            if (_enemy == null || _enemy.IsDead || _enemy.Attributes == null)
            {
                return;
            }

            RemoveDeadChildren();
            UpdatePhase();
            _spawnTimer = Mathf.Max(0f, _spawnTimer - Time.deltaTime);
            _decisionTimer = Mathf.Max(0f, _decisionTimer - Time.deltaTime);

            if (_activeAction == GoapActionId.ApproachPlayer)
            {
                TickPlayerApproach();
                return;
            }

            if (_decisionTimer > 0f)
            {
                return;
            }

            if (TryPlanSpawn())
            {
                return;
            }

            if (TryPlanPlayerApproach())
            {
                return;
            }

            EnemyGoapMotion.ResumeFlocking(_flockBridge);
        }

        /// <summary>母体死亡时让仍存活的子敌人以 ParentDied 原因死亡。</summary>
        /// <param name="mother">触发死亡事件的母体。</param>
        private void HandleMotherDied(Enemy mother)
        {
            if (mother != _enemy || _children.Count == 0)
            {
                return;
            }

            CopyAndDetachChildren();
            for (int index = 0; index < _iterationBuffer.Count; index++)
            {
                Enemy child = _iterationBuffer[index];
                if (child != null && !child.IsDead)
                {
                    child.DieFromHealth(EnemyDeathCause.ParentDied);
                }
            }

            _iterationBuffer.Clear();
        }

        /// <summary>处理子敌人死亡并更新母体数量。</summary>
        /// <param name="child">已死亡的子敌人。</param>
        private void HandleChildDied(Enemy child)
        {
            if (child == null)
            {
                return;
            }

            UnsubscribeChild(child);
            _children.Remove(child);
        }

        /// <summary>移除已经归还对象池并开始新一轮生命的子代。</summary>
        /// <param name="child">已重置的子敌人。</param>
        private void HandleChildReused(Enemy child)
        {
            UnsubscribeChild(child);
            _children.Remove(child);
        }

        /// <summary>移除已死亡或已销毁的子敌人。</summary>
        private void RemoveDeadChildren()
        {
            _iterationBuffer.Clear();
            foreach (Enemy child in _children)
            {
                if (child == null || child.IsDead)
                {
                    _iterationBuffer.Add(child);
                }
            }

            for (int index = 0; index < _iterationBuffer.Count; index++)
            {
                Enemy child = _iterationBuffer[index];
                UnsubscribeChild(child);
                _children.Remove(child);
            }

            _iterationBuffer.Clear();
        }

        /// <summary>规划并尝试生成一个子敌人。</summary>
        /// <returns>本次计划处理了召唤动作时返回 true。</returns>
        private bool TryPlanSpawn()
        {
            EnemyAttributesSO attributes = _enemy.Attributes;
            int targetCount = _phase == EnemyBossPhase.Second
                ? attributes.childPhaseTwoTargetAlive
                : attributes.childPhaseOneTargetAlive;
            targetCount = Mathf.Clamp(targetCount, 0, Mathf.Max(0, attributes.childMaximumAlive));

            if (_childSpawner == null ||
                attributes.childPrefab == null ||
                _spawnTimer > 0f ||
                _children.Count >= targetCount ||
                _children.Count >= Mathf.Max(0, attributes.childMaximumAlive))
            {
                return false;
            }

            GoapFact state = GoapFact.ChildPopulationBelowTarget |
                             GoapFact.ChildSlotAvailable |
                             GoapFact.SpawnReady;
            _actions.Clear();
            _actions.Add(new GoapActionDefinition(
                GoapActionId.SpawnChild,
                state,
                GoapFact.ChildPopulationSatisfied,
                GoapFact.ChildPopulationBelowTarget,
                1f));

            if (!GoapPlanner.TryPlan(state, GoapFact.ChildPopulationSatisfied, _actions, _plan) ||
                _plan.Count == 0 ||
                _plan[0].Id != GoapActionId.SpawnChild)
            {
                _plan.Clear();
                return false;
            }

            _plan.Clear();
            bool spawned = TrySpawnChild();
            _decisionTimer = Mathf.Max(0f, attributes.goapReplanInterval);
            return spawned;
        }

        /// <summary>规划接近玩家，进入接触伤害范围后保持位置。</summary>
        /// <returns>存在玩家规划或有效玩家时返回 true。</returns>
        private bool TryPlanPlayerApproach()
        {
            if (!HasLivePlayer())
            {
                ResetPlan();
                return false;
            }

            float distance = Vector3.Distance(transform.position, _playerTarget.Transform.position);
            GoapFact state = GoapFact.PlayerAvailable;
            if (distance <= _enemy.Attributes.motherPlayerEngageRange)
            {
                state |= GoapFact.PlayerInRange;
            }

            _actions.Clear();
            _actions.Add(new GoapActionDefinition(
                GoapActionId.ApproachPlayer,
                GoapFact.PlayerAvailable,
                GoapFact.PlayerInRange,
                GoapFact.None,
                distance / Mathf.Max(MinimumPlanSpeed, GetBaseSpeed())));

            if (!GoapPlanner.TryPlan(state, GoapFact.PlayerInRange, _actions, _plan))
            {
                ResetPlan();
                return false;
            }

            if (_plan.Count == 0)
            {
                _activeAction = GoapActionId.None;
                EnemyGoapMotion.Hold(_enemy, _flockBridge);
                _decisionTimer = _enemy.Attributes.goapReplanInterval;
                return true;
            }

            _activeAction = _plan[0].Id;
            _plan.Clear();
            TickPlayerApproach();
            return true;
        }

        /// <summary>推进当前接近玩家动作。</summary>
        private void TickPlayerApproach()
        {
            if (!HasLivePlayer())
            {
                ResetPlan();
                EnemyGoapMotion.ResumeFlocking(_flockBridge);
                return;
            }

            bool reached = EnemyGoapMotion.MoveTowards(
                _enemy,
                _flockBridge,
                _playerTarget.Transform.position,
                _enemy.Attributes.motherPlayerEngageRange,
                GetBaseSpeed(),
                GetPhaseSpeedMultiplier(),
                GetTurnSpeed());
            if (reached)
            {
                _activeAction = GoapActionId.None;
                _decisionTimer = _enemy.Attributes.goapReplanInterval;
            }
        }

        /// <summary>生成一个子敌人并在死亡时释放其归属计数。</summary>
        /// <returns>生成成功时返回 true。</returns>
        private bool TrySpawnChild()
        {
            EnemyAttributesSO attributes = _enemy.Attributes;
            Vector2 offset = UnityEngine.Random.insideUnitCircle *
                              Mathf.Max(0f, attributes.childSpawnRadius);
            Vector3 position = transform.position + new Vector3(offset.x, 0f, offset.y);
            Enemy child = _childSpawner.TrySpawnChild(
                _enemy,
                attributes.childPrefab,
                position,
                attributes.childHealthMultiplier,
                attributes.childSpeedMultiplier);
            if (child == null)
            {
                return false;
            }

            _children.Add(child);
            SubscribeChild(child);
            _spawnTimer = _phase == EnemyBossPhase.Second
                ? Mathf.Max(MinimumSpawnInterval, attributes.childPhaseTwoSpawnInterval)
                : Mathf.Max(MinimumSpawnInterval, attributes.childSpawnInterval);
            return true;
        }

        /// <summary>只允许母体从第一阶段切换到第二阶段。</summary>
        private void UpdatePhase()
        {
            if (_phase == EnemyBossPhase.First &&
                _enemy.HealthPercent <= _enemy.Attributes.bossSecondPhaseHealthPercent)
            {
                _phase = EnemyBossPhase.Second;
                ResetPlan();
            }
        }

        /// <summary>检查玩家是否仍可用于接近规划。</summary>
        private bool HasLivePlayer()
        {
            return _playerTarget != null &&
                   _playerTarget.IsAlive &&
                   _playerTarget.Transform != null;
        }

        /// <summary>读取敌人群游配置中的最大速度。</summary>
        private float GetBaseSpeed()
        {
            return _enemy.FlockSettings != null
                ? _enemy.FlockSettings.GetProfileForEnemyType(
                    _enemy.EnemyType,
                    _enemy.SpeedMultiplier).MaxSpeed
                : 0f;
        }

        /// <summary>读取群游配置中的最大转向速度。</summary>
        private float GetTurnSpeed()
        {
            return _enemy.FlockSettings != null
                ? _enemy.FlockSettings.GetProfileForEnemyType(
                    _enemy.EnemyType,
                    _enemy.SpeedMultiplier).RotationSpeed
                : 0f;
        }

        /// <summary>取得当前阶段的移动速度倍率。</summary>
        private float GetPhaseSpeedMultiplier()
        {
            return _phase == EnemyBossPhase.Second
                ? _enemy.Attributes.bossPhaseTwoSpeedMultiplier
                : 1f;
        }

        /// <summary>复制并解除全部子敌人订阅，避免死亡回调修改遍历集合。</summary>
        private void CopyAndDetachChildren()
        {
            _iterationBuffer.Clear();
            foreach (Enemy child in _children)
            {
                _iterationBuffer.Add(child);
                UnsubscribeChild(child);
            }

            _children.Clear();
        }

        /// <summary>解除全部子代死亡订阅并清空归属集合。</summary>
        private void ClearChildren()
        {
            foreach (Enemy child in _children)
            {
                UnsubscribeChild(child);
            }

            _children.Clear();
            _childReuseHandlers.Clear();
            _iterationBuffer.Clear();
        }

        /// <summary>订阅子敌人的死亡与池化重置事件。</summary>
        private void SubscribeChild(Enemy child)
        {
            child.OnDied += HandleChildDied;
            Action reuseHandler = () => HandleChildReused(child);
            _childReuseHandlers.Add(child, reuseHandler);
            child.ReuseRequested += reuseHandler;
        }

        /// <summary>解除子敌人事件订阅。</summary>
        private void UnsubscribeChild(Enemy child)
        {
            if (ReferenceEquals(child, null))
            {
                return;
            }

            if (child != null)
            {
                child.OnDied -= HandleChildDied;
            }

            if (_childReuseHandlers.Remove(child, out Action reuseHandler))
            {
                if (child != null)
                {
                    child.ReuseRequested -= reuseHandler;
                }
            }
        }

        /// <summary>清除当前动作和规划缓存。</summary>
        private void ResetPlan()
        {
            _activeAction = GoapActionId.None;
            _actions.Clear();
            _plan.Clear();
            _decisionTimer = 0f;
        }

        /// <summary>订阅母体死亡事件，避免重复注册。</summary>
        private void SubscribeParentDeath()
        {
            if (_enemy != null && !_parentDeathSubscribed)
            {
                _enemy.OnDied += HandleMotherDied;
                _parentDeathSubscribed = true;
            }
        }

        /// <summary>解除母体死亡事件订阅。</summary>
        private void UnsubscribeParentDeath()
        {
            if (_enemy != null && _parentDeathSubscribed)
            {
                _enemy.OnDied -= HandleMotherDied;
                _parentDeathSubscribed = false;
            }
        }

        /// <summary>补齐实体和 ECS 移动桥接引用。</summary>
        private void CacheComponents()
        {
            _enemy ??= GetComponent<Enemy>();
            _flockBridge ??= GetComponent<EnemyFlockBridge>();
        }

        /// <summary>租借时重置阶段、计时、计划和子代归属。</summary>
        public void OnRentFromPool()
        {
            CacheComponents();
            ClearChildren();
            ResetPlan();
            _spawnTimer = 0f;
            _phase = EnemyBossPhase.First;
            SubscribeParentDeath();
        }

        /// <summary>归还时解除订阅并清空本轮状态。</summary>
        public void OnReturnToPool()
        {
            UnsubscribeParentDeath();
            ClearChildren();
            ResetPlan();
            _spawnTimer = 0f;
            _phase = EnemyBossPhase.First;
        }

        /// <summary>注入完成后缓存实体依赖并恢复死亡监听。</summary>
        void IEnemyActivationListener.OnEnemyActivationReady()
        {
            CacheComponents();
            SubscribeParentDeath();
        }
    }
}
