using System;
using System.Collections.Generic;
using UnityEngine;
using FlockingSystem;
using Services;
using Interfaces;
using SpatialSystem.Bridge;
using VContainer;
using CombatSystem;
using UnityEngine.Serialization;

namespace EnemySystem
{
    /// <summary>
    /// 敌人实体 - 单类(无子类)。
    /// 行为差异(Fast 闪避 / Tank 护盾+爆炸)通过 <see cref="EnemyDodgeComponent"/> 等组件挂载实现。
    /// 配置仅由 <see cref="EnemyAttributes"/> SO 承载(HP + Speed + 必要 flock 参数)。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class Enemy : MonoBehaviour, ILockable, IBuffable, IDamageReceiver
    {
        [FormerlySerializedAs("stats")]
        [Header("基础数据")]
        [Tooltip("敌人属性 SO(HP/Speed/Flocking)")]
        [SerializeField] private EnemyAttributes attributes;

        [Header("调试")]
        [SerializeField] private bool showDebugInfo = false;

        // === 运行时状态 ===
        protected bool isDead = false;
        protected Collider enemyCollider;
        protected FlockAgent flockAgent;
        protected BuffController _buffController;
        protected int spatialEntityId = -1;

        public EnemyType EnemyType => attributes != null ? attributes.enemyType : EnemyType.Normal;

        public float CurrentHealth { get; protected set; }
        public float MaxHealth { get; protected set; }
        public bool IsDead => isDead;
        public float HealthPercent => MaxHealth > 0f ? CurrentHealth / MaxHealth : 0f;

        public float SpeedMultiplier { get; set; } = 1f;
        public BuffController BuffController => _buffController;

        public GameObject SourcePrefab { get; set; }

        /// <summary>敌人死亡事件 - 由 <see cref="EnemySpawnManager"/> 订阅以扣减存活数。</summary>
        public event Action<Enemy> OnDied;

        /// <summary>
        /// 伤害前置拦截委托。返回 false 表示完全闪避；返回 true 表示继续处理。
        /// </summary>
        public delegate bool EnemyDamageInterceptor(Enemy enemy, float originalDamage, ref float finalDamage);

        private readonly List<EnemyDamageInterceptor> _preDamageInterceptors = new(2);

        [Inject] protected ISpatialQueryService _spatialService;
        [Inject] protected IEffectService _effectService;

        // === IDamageable ===
        Vector3 IDamageable.Position => transform.position;
        bool IDamageable.IsAlive => !isDead && gameObject.activeInHierarchy;
        Transform IDamageable.Transform => transform;

        // === ILockable ===
        float ILockable.ThreatLevel
        {
            get
            {
                float baseThreat = EnemyType switch
                {
                    EnemyType.Tank => 60f,
                    EnemyType.Fast => 40f,
                    _ => 30f
                };
                float healthFactor = MaxHealth > 0f ? CurrentHealth / MaxHealth : 1f;
                return Mathf.Clamp(baseThreat * (0.5f + 0.5f * healthFactor), 0f, 100f);
            }
        }
        bool ILockable.IsLockable => !isDead && gameObject.activeInHierarchy;
        Vector3 ILockable.LockAnchorPoint => transform.position + Vector3.up * 2f;
        TargetCategory ILockable.Category => EnemyType == EnemyType.Tank ? TargetCategory.Boss : TargetCategory.Normal;
        float ILockable.HealthPercent => HealthPercent;

        #region Unity 生命周期

        protected virtual void Awake()
        {
            enemyCollider = GetComponent<Collider>();
            flockAgent = GetComponent<FlockAgent>() ?? gameObject.AddComponent<FlockAgent>();
            _buffController = GetComponent<BuffController>() ?? gameObject.AddComponent<BuffController>();
        }

        protected virtual void Start()
        {
            if (MaxHealth <= 0f && attributes != null)
                ApplyStats(1f, 1f);
        }

        protected virtual void OnEnable()
        {
            if (_spatialService != null)
            {
                spatialEntityId = _spatialService.Register(
                    this, flockAgent != null ? flockAgent.NeighbourDistance : 5f,
                    SpatialRegistry.LAYER_ENEMY);
            }
        }

        protected virtual void OnDisable()
        {
            if (_spatialService != null && spatialEntityId >= 0)
            {
                _spatialService.Unregister(spatialEntityId);
                spatialEntityId = -1;
            }
        }

        protected virtual void Update()
        {
            if (isDead) return;
            if (flockAgent != null)
                flockAgent.SpeedMultiplier = SpeedMultiplier;
            UpdateSpatialPosition();
        }

        protected virtual void OnDestroy()
        {
        }

        protected virtual void OnDrawGizmosSelected()
        {
            if (!showDebugInfo) return;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 1f);
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 由 <see cref="EnemySpawnManager"/> 在生成后立即调用。
        /// </summary>
        /// <param name="hpMult">波次血量倍率</param>
        /// <param name="speedMult">波次速度倍率</param>
        public virtual void ApplyStats(float hpMult, float speedMult)
        {
            if (attributes == null)
            {
                Debug.LogError($"[Enemy] {name}: EnemyStats 未配置!", this);
                return;
            }

            float scaledMaxHp = attributes.baseHealth * Mathf.Max(0.01f, hpMult);
            MaxHealth = scaledMaxHp;
            CurrentHealth = scaledMaxHp;

            SpeedMultiplier = speedMult;

            if (flockAgent != null)
            {
                flockAgent.NeighbourDistance = attributes.flockNeighbourDistance;
                flockAgent.SeparationDistance = attributes.flockSeparationDistance;
                flockAgent.RotationSpeed = attributes.flockRotationSpeed;
            }
        }

        #endregion

        #region 战斗

        public virtual void TakeDamage(float damage)
        {
            var request = new DamageRequest
            {
                BaseDamage = damage,
                DamageType = DamageType.Physical,
                HitPoint = transform.position,
                HitNormal = transform.position.normalized
            };
            ReceiveDamage(request);
        }

        public virtual DamageResult ReceiveDamage(in DamageRequest request)
        {
            if (isDead)
            {
                return new DamageResult
                {
                    Outcome = DamageOutcome.Invalid,
                    RemainingHealth = CurrentHealth
                };
            }

            float finalDamage = Mathf.Max(0f, request.BaseDamage);
            for (int index = 0; index < _preDamageInterceptors.Count; index++)
            {
                EnemyDamageInterceptor interceptor = _preDamageInterceptors[index];
                if (!interceptor(this, request.BaseDamage, ref finalDamage))
                {
                    return new DamageResult
                    {
                        Outcome = DamageOutcome.Dodged,
                        RemainingHealth = CurrentHealth
                    };
                }
            }

            float buffMultiplier = _buffController != null ? _buffController.GetModifier(BuffType.DamageTakenMultiplier) : 1f;
            float effectiveDamage = Mathf.Max(0f, finalDamage * buffMultiplier);
            if (effectiveDamage <= 0f)
            {
                return new DamageResult
                {
                    Outcome = DamageOutcome.Blocked,
                    RemainingHealth = CurrentHealth
                };
            }

            CurrentHealth -= effectiveDamage;

            _effectService?.Play("EnemyHit", transform.position);
            OnDamageTaken(effectiveDamage);

            bool isKill = false;
            if (CurrentHealth <= 0f)
            {
                Die();
                isKill = true;
            }

            return new DamageResult
            {
                Outcome = isKill ? DamageOutcome.Killed : DamageOutcome.Applied,
                ActualDamage = effectiveDamage,
                RemainingHealth = Mathf.Max(0f, CurrentHealth),
                IsKill = isKill
            };
        }

        /// <summary>注册伤害前置拦截器。重复注册同一委托会被忽略。</summary>
        public void RegisterPreDamageInterceptor(EnemyDamageInterceptor interceptor)
        {
            if (interceptor != null && !_preDamageInterceptors.Contains(interceptor))
                _preDamageInterceptors.Add(interceptor);
        }

        /// <summary>注销此前注册的伤害前置拦截器。</summary>
        public void UnregisterPreDamageInterceptor(EnemyDamageInterceptor interceptor)
        {
            if (interceptor != null)
                _preDamageInterceptors.Remove(interceptor);
        }

        protected virtual void OnDamageTaken(float damage) { }

        protected virtual void Die()
        {
            if (isDead) return;
            isDead = true;

            if (enemyCollider != null)
                enemyCollider.enabled = false;

            _effectService?.Play("EnemyDeath", transform.position);
            OnDeath();

            OnDied?.Invoke(this);
        }

        /// <summary>
        /// 外部击杀入口(如清屏技能)。走完整死亡链路:特效/积分/加时/OnDied。
        /// 与 TakeDamage 不同,不会被闪避/护盾等前置拦截。
        /// </summary>
        public void Kill()
        {
            Die();
        }

        protected virtual void OnDeath()
        {
        }

        public virtual void ResetForReuse()
        {
            isDead = false;
            if (enemyCollider != null)
                enemyCollider.enabled = true;
            _buffController?.RemoveAll();
            CurrentHealth = 0f;
            MaxHealth = 0f;
            SpeedMultiplier = 1f;
        }

        #endregion

        #region 空间

        private void UpdateSpatialPosition()
        {
            if (_spatialService != null && spatialEntityId >= 0)
                _spatialService.UpdatePosition(spatialEntityId, transform.position);
        }

        #endregion

        #region IBuffable

        public void ApplyBuff(BuffDefinition config)
        {
            ApplyBuff(config, 0);
        }

        public void ApplyBuff(BuffDefinition config, int sourceId)
        {
            _buffController?.AddBuff(config, sourceId);
        }

        #endregion
    }
}
