using System.Collections.Generic;
using EnemySystem.GOAP;
using FlockingSystem.ECS;
using Interfaces;
using Services;
using SpatialIndexing;
using UnityEngine;
using Utils;
using VContainer;

namespace EnemySystem.Components
{
    /// <summary>通过 GOAP 选择猎物并规划接近、吞噬或压迫玩家。</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    [RequireComponent(typeof(EnemyFlockBridge))]
    public sealed class EnemyGrowthComponent : MonoBehaviour, IPooledObject, IEnemyActivationListener
    {
        private const float MinimumPlanSpeed = 0.01f;
        private const float ConsumeActionCost = 0.1f;

        [Inject] private ISpatialQueryService _spatialQuery;
        [Inject] private IPlayerCombatTarget _playerTarget;

        private readonly List<GoapActionDefinition> _actions = new(2);
        private readonly List<GoapActionDefinition> _plan = new(2);
        private Enemy _enemy;
        private EnemyFlockBridge _flockBridge;
        private Enemy _target;
        private GoapActionId _activeAction;
        private EnemyBossPhase _phase;
        private float _decisionTimer;

        /// <summary>缓存宿主和 ECS 移动桥接。</summary>
        private void Awake()
        {
            CacheComponents();
        }

        /// <summary>启用时清空上一轮目标、计划和 Boss 阶段。</summary>
        private void OnEnable()
        {
            CacheComponents();
            ResetPlan();
        }

        /// <summary>停用时清除计划并恢复存活敌人的群游控制。</summary>
        private void OnDisable()
        {
            ResetPlan();
            if (gameObject.activeInHierarchy && (_enemy == null || !_enemy.IsDead))
            {
                EnemyGoapMotion.ResumeFlocking(_flockBridge);
            }
        }

        /// <summary>更新阶段、推进当前动作或选择新计划。</summary>
        private void Update()
        {
            if (_enemy == null || _enemy.IsDead || _enemy.Attributes == null)
            {
                return;
            }

            UpdatePhase();
            _decisionTimer = Mathf.Max(0f, _decisionTimer - Time.deltaTime);

            if (TickActiveAction())
            {
                return;
            }

            if (_decisionTimer > 0f)
            {
                return;
            }

            PlanNextAction();
        }

        /// <summary>对指定目标执行一次吞噬动作并转移其全部当前生命。</summary>
        /// <param name="target">GOAP 选中的目标。</param>
        /// <returns>目标存活、可吞噬且吸收成功时返回 true。</returns>
        private bool TryAbsorbTarget(Enemy target)
        {
            if (!IsEdible(target))
            {
                return false;
            }

            return _enemy.TryAbsorb(target);
        }

        /// <summary>租借时清除上一轮计划并恢复第一阶段。</summary>
        public void OnRentFromPool()
        {
            CacheComponents();
            ResetPlan();
            _phase = EnemyBossPhase.First;
        }

        /// <summary>归还对象池时清空目标、计划和运动模式。</summary>
        public void OnReturnToPool()
        {
            ResetPlan();
            _phase = EnemyBossPhase.First;
            if (_enemy == null || !_enemy.IsDead)
            {
                EnemyGoapMotion.ResumeFlocking(_flockBridge);
            }
        }

        /// <summary>补齐容器注入后的运行时组件引用。</summary>
        void IEnemyActivationListener.OnEnemyActivationReady()
        {
            CacheComponents();
        }

        /// <summary>执行当前动作；返回 true 表示本帧由该动作拥有行为。</summary>
        private bool TickActiveAction()
        {
            if (_activeAction == GoapActionId.ApproachPrey)
            {
                if (!IsEdible(_target))
                {
                    ResetPlan();
                    EnemyGoapMotion.ResumeFlocking(_flockBridge);
                    return true;
                }

                bool reached = EnemyGoapMotion.MoveTowards(
                    _enemy,
                    _flockBridge,
                    _target.transform.position,
                    _enemy.Attributes.growthAbsorbRange,
                    GetBaseSpeed(),
                    GetPhaseSpeedMultiplier(),
                    GetTurnSpeed());
                if (reached)
                {
                    _activeAction = GoapActionId.None;
                    _decisionTimer = 0f;
                }

                return true;
            }

            if (_activeAction == GoapActionId.ApproachPlayer)
            {
                if (!HasLivePlayer())
                {
                    ResetPlan();
                    EnemyGoapMotion.ResumeFlocking(_flockBridge);
                    return true;
                }

                bool reached = EnemyGoapMotion.MoveTowards(
                    _enemy,
                    _flockBridge,
                    _playerTarget.Transform.position,
                    _enemy.Attributes.growthPlayerEngageRange,
                    GetBaseSpeed(),
                    GetPhaseSpeedMultiplier(),
                    GetTurnSpeed());
                if (reached)
                {
                    _activeAction = GoapActionId.None;
                    _decisionTimer = _enemy.Attributes.goapReplanInterval;
                }

                return true;
            }

            return false;
        }

        /// <summary>根据可吞噬目标或玩家接触目标建立最低代价动作序列。</summary>
        private void PlanNextAction()
        {
            _target = FindEdibleTarget();
            if (_target != null)
            {
                PlanPreyAction();
                return;
            }

            PlanPlayerAction();
        }

        /// <summary>规划接近并吞噬最近的合格目标。</summary>
        private void PlanPreyAction()
        {
            float distance = Vector3.Distance(transform.position, _target.transform.position);
            GoapFact state = GoapFact.PreyAvailable;
            if (distance <= _enemy.Attributes.growthAbsorbRange)
            {
                state |= GoapFact.PreyInRange;
            }

            _actions.Clear();
            _actions.Add(new GoapActionDefinition(
                GoapActionId.ApproachPrey,
                GoapFact.PreyAvailable,
                GoapFact.PreyInRange,
                GoapFact.None,
                distance / Mathf.Max(MinimumPlanSpeed, GetBaseSpeed())));
            _actions.Add(new GoapActionDefinition(
                GoapActionId.ConsumePrey,
                GoapFact.PreyAvailable | GoapFact.PreyInRange,
                GoapFact.PreyConsumed,
                GoapFact.PreyAvailable | GoapFact.PreyInRange,
                ConsumeActionCost));

            if (!GoapPlanner.TryPlan(state, GoapFact.PreyConsumed, _actions, _plan) || _plan.Count == 0)
            {
                EnemyGoapMotion.ResumeFlocking(_flockBridge);
                _decisionTimer = _enemy.Attributes.goapReplanInterval;
                return;
            }

            _activeAction = _plan[0].Id;
            _plan.Clear();
            if (_activeAction == GoapActionId.ConsumePrey)
            {
                TryAbsorbTarget(_target);
                ResetPlan();
                _decisionTimer = _enemy.Attributes.goapReplanInterval;
                return;
            }

            TickActiveAction();
        }

        /// <summary>没有猎物时规划接近玩家并维持接触压力。</summary>
        private void PlanPlayerAction()
        {
            if (!HasLivePlayer())
            {
                ResetPlan();
                _decisionTimer = _enemy.Attributes.goapReplanInterval;
                EnemyGoapMotion.ResumeFlocking(_flockBridge);
                return;
            }

            float distance = Vector3.Distance(transform.position, _playerTarget.Transform.position);
            GoapFact state = GoapFact.PlayerAvailable;
            if (distance <= _enemy.Attributes.growthPlayerEngageRange)
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
                EnemyGoapMotion.ResumeFlocking(_flockBridge);
                _decisionTimer = _enemy.Attributes.goapReplanInterval;
                return;
            }

            if (_plan.Count == 0)
            {
                EnemyGoapMotion.Hold(_enemy, _flockBridge);
                _decisionTimer = _enemy.Attributes.goapReplanInterval;
                return;
            }

            _activeAction = _plan[0].Id;
            _plan.Clear();
            TickActiveAction();
        }

        /// <summary>读取空间查询结果并筛选最近的 Normal 或 Child 敌人。</summary>
        private Enemy FindEdibleTarget()
        {
            if (_spatialQuery == null)
            {
                return null;
            }

            List<IDamageable> candidates = _spatialQuery.QueryRadius(
                transform.position,
                _enemy.Attributes.growthTargetSearchRadius,
                SpatialLayers.Enemy);
            Enemy nearest = null;
            float nearestDistanceSquared = float.PositiveInfinity;

            for (int index = 0; index < candidates.Count; index++)
            {
                if (candidates[index] is not Enemy candidate || !IsEdible(candidate))
                {
                    continue;
                }

                float distanceSquared = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearest = candidate;
                    nearestDistanceSquared = distanceSquared;
                }
            }

            return nearest;
        }

        /// <summary>拒绝自身、死亡目标及精英/Boss 类别。</summary>
        private bool IsEdible(Enemy target)
        {
            return target != null &&
                   target != _enemy &&
                   !target.IsDead &&
                   target.CurrentHealth > 0f &&
                   (target.SpawnCategory == EnemySpawnCategory.Normal ||
                    target.SpawnCategory == EnemySpawnCategory.Child);
        }

        /// <summary>只允许 Boss 从第一阶段向第二阶段切换。</summary>
        private void UpdatePhase()
        {
            if (_phase == EnemyBossPhase.First &&
                _enemy.HealthPercent <= _enemy.Attributes.bossSecondPhaseHealthPercent)
            {
                _phase = EnemyBossPhase.Second;
            }
        }

        /// <summary>检查玩家目标当前是否有效。</summary>
        private bool HasLivePlayer()
        {
            return _playerTarget != null &&
                   _playerTarget.IsAlive &&
                   _playerTarget.Transform != null;
        }

        /// <summary>读取敌人类型群游配置中的最大速度。</summary>
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

        /// <summary>取得第二阶段的移动速度倍率。</summary>
        private float GetPhaseSpeedMultiplier()
        {
            return _phase == EnemyBossPhase.Second
                ? _enemy.Attributes.bossPhaseTwoSpeedMultiplier
                : 1f;
        }

        /// <summary>清空 GOAP 计划和当前动作。</summary>
        private void ResetPlan()
        {
            _target = null;
            _activeAction = GoapActionId.None;
            _actions.Clear();
            _plan.Clear();
            _decisionTimer = 0f;
        }

        /// <summary>缓存实体宿主及运动桥接。</summary>
        private void CacheComponents()
        {
            _enemy ??= GetComponent<Enemy>();
            _flockBridge ??= GetComponent<EnemyFlockBridge>();
        }
    }
}
