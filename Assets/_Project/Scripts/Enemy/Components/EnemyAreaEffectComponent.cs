using System.Collections.Generic;
using CombatSystem;
using EnemySystem.AreaEffect;
using Interfaces;
using Services;
using SpatialIndexing;
using UnityEngine;
using Utils;
using VContainer;

namespace EnemySystem.Components
{
    /// <summary>
    /// 为范围效果源保存配置和目标缓存。
    /// <para>实际调度由 EnemyAreaEffectService 统一驱动，本组件不再拥有逐帧 Update。</para>
    /// <para>范围只决定可以新增哪些目标；已施加的 Buff 按自身持续时间独立结束。</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    public sealed class EnemyAreaEffectComponent : MonoBehaviour, IPooledObject, IEnemyActivationListener
    {
        private const int MaximumAffectedTargets = 50;

        [Inject] private ISpatialQueryService _spatialService;
        [Inject] private IPlayerCombatTarget _playerTarget;
        [Inject] private EnemyAreaEffectService _areaEffectService;

        private readonly List<IDamageable> _candidateTargets = new(MaximumAffectedTargets);
        private readonly List<AffectedTarget> _affectedTargets = new(MaximumAffectedTargets);

        private Enemy _enemy;
        private BuffDefinitionSO _runtimeSlowBuff;
        private BuffDefinitionSO _effectBuff;
        private EnemyAreaEffectType _effectType;
        private float _effectRadius;
        private float _effectRadiusSquared;
        private float _queryInterval;
        private float _queryTimer;
        private int _candidateCursor;
        private bool _configurationCached;
        private bool _registered;

        private struct AffectedTarget
        {
            public IDamageable Target;
            public float ExpireAt;
        }

        /// <summary>缓存宿主敌人并清空范围状态。</summary>
        private void Awake()
        {
            TryGetComponent(out _enemy);
            ResetState();
        }

        /// <summary>启用时允许立即刷新候选，并尝试登记到集中调度器。</summary>
        private void OnEnable()
        {
            ResetState();
            TryRegister();
        }

        /// <summary>禁用时注销调度器并清理本次租借的候选状态。</summary>
        private void OnDisable()
        {
            Unregister();
            ResetState();
        }

        /// <summary>释放本组件创建的运行时减速定义。</summary>
        private void OnDestroy()
        {
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

        /// <summary>
        /// 处理一个范围源的当前帧预算。
        /// </summary>
        /// <param name="deltaTime">当前帧经过的秒数。</param>
        internal void ProcessFrame(float deltaTime)
        {
            if (_enemy == null)
            {
                TryGetComponent(out _enemy);
            }

            if (!isActiveAndEnabled || _enemy == null || _enemy.IsDead)
            {
                return;
            }

            CacheConfiguration();
            if (_effectBuff == null)
            {
                return;
            }

            PruneAffectedTargets();
            _queryTimer -= Mathf.Max(0f, deltaTime);
            if (_queryTimer <= 0f)
            {
                RefreshCandidates();
                _queryTimer = _queryInterval - Mathf.Max(0f, deltaTime);
            }

            TryApplyNextTarget();
        }

        /// <summary>对象池租借时重置目标游标和范围状态。</summary>
        public void OnRentFromPool()
        {
            ResetState();
        }

        /// <summary>对象池归还时重置目标游标和范围状态。</summary>
        public void OnReturnToPool()
        {
            ResetState();
        }

        /// <summary>补做宿主激活后的依赖缓存和调度登记。</summary>
        void IEnemyActivationListener.OnEnemyActivationReady()
        {
            if (_enemy == null)
            {
                TryGetComponent(out _enemy);
            }

            TryRegister();
        }

        /// <summary>登记当前范围源，兼容注入先后顺序和对象池重复激活。</summary>
        private void TryRegister()
        {
            if (_enemy == null)
            {
                TryGetComponent(out _enemy);
            }

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
            if (_registered)
            {
                _areaEffectService?.Unregister(this);
                _registered = false;
            }
        }

        /// <summary>读取一次本次租借的范围参数和对应 Buff。</summary>
        private void CacheConfiguration()
        {
            if (_configurationCached || _enemy == null || _enemy.Attributes == null)
            {
                return;
            }

            EnemyAttributesSO attributes = _enemy.Attributes;
            _effectType = attributes.areaEffectType;
            _effectRadius = Mathf.Max(0f, attributes.areaEffectRadius);
            _effectRadiusSquared = _effectRadius * _effectRadius;
            _queryInterval = Mathf.Max(0.01f, attributes.areaEffectTickInterval);
            _effectBuff = ResolveEffectBuff(attributes);
            _configurationCached = true;
        }

        /// <summary>按范围模式返回减速或持续治疗 Buff。</summary>
        private BuffDefinitionSO ResolveEffectBuff(EnemyAttributesSO attributes)
        {
            return _effectType == EnemyAreaEffectType.Slow
                ? GetSlowBuff(attributes)
                : attributes.areaHealBuff;
        }

        /// <summary>刷新当前范围内的敌人候选，结果写入本组件的复用缓存。</summary>
        private void RefreshCandidates()
        {
            _candidateTargets.Clear();
            _candidateCursor = 0;
            if (_spatialService == null)
            {
                return;
            }

            List<IDamageable> targets = _spatialService.QueryRadius(
                transform.position,
                _effectRadius,
                SpatialLayers.Enemy);
            int targetCount = Mathf.Min(MaximumAffectedTargets, targets.Count);
            for (int index = 0; index < targetCount; index++)
            {
                IDamageable target = targets[index];
                if (target != null)
                {
                    _candidateTargets.Add(target);
                }
            }
        }

        /// <summary>扫描候选并在本帧成功施加一个 Buff 后停止。</summary>
        private void TryApplyNextTarget()
        {
            if (_affectedTargets.Count < MaximumAffectedTargets &&
                TryApplyToEnemy())
            {
                return;
            }

            TryApplyToPlayer();
        }

        /// <summary>从游标开始跳过已有同类 Buff 的敌人，直到成功施加一次。</summary>
        private bool TryApplyToEnemy()
        {
            int count = _candidateTargets.Count;
            if (count == 0)
            {
                return false;
            }

            int start = _candidateCursor % count;
            for (int offset = 0; offset < count; offset++)
            {
                int index = (start + offset) % count;
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
                _candidateCursor = (index + 1) % count;
                return true;
            }

            _candidateCursor = (start + 1) % count;
            return false;
        }

        /// <summary>按当前模式尝试给范围内玩家施加一次 Buff。</summary>
        private bool TryApplyToPlayer()
        {
            if (_effectType == EnemyAreaEffectType.HealEnemies ||
                !IsTargetInRange(_playerTarget) ||
                _playerTarget is not IBuffReceiver receiver)
            {
                return false;
            }

            return receiver.TryApplyBuffIfAbsent(_effectBuff, GetInstanceID());
        }

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
        private bool IsTargetInRange(IDamageable target)
        {
            return IsAlive(target) &&
                (target.Position - transform.position).sqrMagnitude <= _effectRadiusSquared;
        }

        /// <summary>计算本次受影响目标槽位的自然过期时间。</summary>
        private float GetExpireAt()
        {
            float duration = _effectBuff != null ? _effectBuff.DurationSeconds : 0f;
            return duration > 0f ? Time.time + duration : float.PositiveInfinity;
        }

        /// <summary>判断目标接口引用仍对应有效的 Unity 对象。</summary>
        private static bool IsAlive(IDamageable target)
        {
            return target != null &&
                (!(target is Object unityObject) || unityObject != null) &&
                target.IsAlive;
        }

        /// <summary>按当前敌人配置生成一个可调整倍率的运行时减速定义。</summary>
        private BuffDefinitionSO GetSlowBuff(EnemyAttributesSO attributes)
        {
            if (attributes == null || attributes.areaSlowBuff == null)
            {
                return null;
            }

            if (_runtimeSlowBuff == null)
            {
                _runtimeSlowBuff = ScriptableObject.CreateInstance<BuffDefinitionSO>();
                _runtimeSlowBuff.name = $"{name}_SlowBuff";
                _runtimeSlowBuff.ModifierType = attributes.areaSlowBuff.ModifierType;
                _runtimeSlowBuff.StackGroupId = attributes.areaSlowBuff.StackGroupId;
                _runtimeSlowBuff.Polarity = attributes.areaSlowBuff.Polarity;
                _runtimeSlowBuff.DurationSeconds = attributes.areaSlowBuff.DurationSeconds;
                _runtimeSlowBuff.HealingPerSecond = attributes.areaSlowBuff.HealingPerSecond;
                _runtimeSlowBuff.StackPolicy = attributes.areaSlowBuff.StackPolicy;
                _runtimeSlowBuff.MaximumStackCount = attributes.areaSlowBuff.MaximumStackCount;
                _runtimeSlowBuff.VisualEffectPrefab = attributes.areaSlowBuff.VisualEffectPrefab;
            }

            _runtimeSlowBuff.ModifierValue = Mathf.Clamp(
                attributes.areaSlowMultiplier,
                0f,
                1f);
            return _runtimeSlowBuff;
        }

        /// <summary>清除本次租借的候选、受影响目标和时间状态。</summary>
        private void ResetState()
        {
            _candidateTargets.Clear();
            _affectedTargets.Clear();
            _candidateCursor = 0;
            _queryTimer = 0f;
            _configurationCached = false;
            _effectBuff = null;
        }
    }
}
