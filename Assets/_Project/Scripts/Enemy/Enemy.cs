using System;
using CombatSystem;
using EnemySystem.Components;
using FlockingSystem.ECS;
using Interfaces;
using Services;
using SpatialIndexing;
using UnityEngine;
using UnityEngine.Serialization;
using Utils;
using VContainer;

namespace EnemySystem
{
    /// <summary>
    /// 敌人实体宿主和组合根。
    /// <para>宿主保存身份、静态配置、生命周期事件，并把生命、表现和空间行为委托给组件。</para>
    /// <para>EnemyFlockBridge 是实体运行所需的桥接组件，由 RequireComponent 明确声明；Buff 状态由场景级 BuffService 统一管理。</para>
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyFlockBridge))]
    [RequireComponent(typeof(EnemyHealthComponent))]
    [RequireComponent(typeof(EnemyDamagePresentationComponent))]
    public class Enemy : MonoBehaviour, ILockable, ICombatTarget, IHealable, IControlResistant, IBuffTarget, IBuffReceiver, IDamageRequestReceiver, IEnemyMotionEffectTarget, IPermanentEnemyMotionEffectTarget, IPooledObject
    {
        private const float LockAnchorPadding = 0.15f;

        [FormerlySerializedAs("stats")]
        [FormerlySerializedAs("attributes")]
        [Header("实体配置")]
        [Tooltip("敌人实体配置(生命值/类型/行为参数)")]
        [SerializeField] private EnemyAttributesSO enemyAttributes;

        [Header("调试")]
        [SerializeField] private bool showDebugInfo;

        private bool _isDead;
        private EnemyHealthComponent _healthComponent;
        private EnemyFlockBridge _flockBridge;
        private EnemyDamagePresentationComponent _damagePresentationComponent;
        private EnemyBuffMotionEffects _buffMotionEffects;
        private EnemyActivationNotifier _activationNotifier;
        private Collider _enemyCollider;
        private Renderer[] _lockAnchorRenderers;
        private EnemySpawnCategory _spawnCategory = EnemySpawnCategory.Normal;

        [Inject] private IBuffService _buffService;
        [Inject] private SpatialQueryService _spatialService;
        [Inject] private EnemyFlockSettingsSO _flockSettings;

        /// <summary>查询服务槽位；不参与 Flock slot 的分配和回收。</summary>
        internal int SpatialSlot { get; private set; } = -1;

        /// <summary>敌人设计编号。</summary>
        public int EnemyId => enemyAttributes != null ? Mathf.Max(1, enemyAttributes.enemyId) : 1;

        /// <summary>敌人类型。</summary>
        public EnemyType EnemyType => enemyAttributes != null
            ? enemyAttributes.enemyType
            : EnemyType.Normal;

        /// <summary>本次生成实例所属分类。</summary>
        internal EnemySpawnCategory SpawnCategory => _spawnCategory;

        /// <summary>本次死亡的原因。</summary>
        public EnemyDeathCause DeathCause { get; private set; } = EnemyDeathCause.None;

        /// <summary>实体静态配置，仅供 Enemy 组件协作使用。</summary>
        internal EnemyAttributesSO Attributes => enemyAttributes;

        /// <summary>当前实体是否具备静态配置。</summary>
        internal bool HasConfiguration => enemyAttributes != null;

        /// <summary>当前生命值。</summary>
        public float CurrentHealth => _healthComponent != null ? _healthComponent.CurrentHealth : 0f;

        /// <summary>本次生成的最大生命值。</summary>
        public float MaxHealth => _healthComponent != null ? _healthComponent.MaxHealth : 0f;

        /// <summary>实体是否已经死亡。</summary>
        public bool IsDead => _isDead;

        /// <summary>当前生命百分比。</summary>
        public float HealthPercent => MaxHealth > 0f ? CurrentHealth / MaxHealth : 0f;

        /// <summary>当前生成使用的速度倍率。</summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>生成倍率与当前移动速度 Buff 合成后的有效倍率。</summary>
        internal float GetMovementSpeedMultiplier() =>
            GetBuffMotionEffects().GetMovementSpeedMultiplier();

        /// <summary>创建该实体的源预制体。</summary>
        public GameObject SourcePrefab { get; set; }

        /// <summary>敌人死亡事件，由生成器和独立死亡机制订阅。</summary>
        public event Action<Enemy> OnDied;

        /// <summary>任何敌人实际扣血后触发。</summary>
        public static event Action<Enemy, DamageResult> AnyDamageApplied;

        /// <summary>
        /// 伤害前置拦截委托。返回 false 表示完全闪避，返回 true 表示继续处理。
        /// </summary>
        public delegate bool EnemyDamageInterceptor(
            Enemy enemy,
            float originalDamage,
            ref float finalDamage);

        /// <summary>生命组件开始处理有效伤害时触发。</summary>
        internal event Action<Enemy, float> DamageTaken;

        /// <summary>实体开始死亡流程时触发，表现组件据此先应用死亡高亮。</summary>
        internal event Action<Enemy> DeathStarted;

        /// <summary>实体被重置为可重新生成状态时触发。</summary>
        internal event Action ReuseRequested;

        /// <summary>所有实体组件完成启用后触发，供已挂载机制完成初始化。</summary>
        internal event Action ActivationReady;

        /// <summary>兼容既有手动注入测试读取群游配置。</summary>
        internal EnemyFlockSettingsSO FlockSettings => _flockSettings;

        /// <summary>死亡表现组件提供的高亮持续时间。</summary>
        internal float DeathHighlightDuration => _damagePresentationComponent != null
            ? _damagePresentationComponent.DeathHighlightDuration
            : 0f;

        /// <summary>初始化实体依赖引用。</summary>
        protected virtual void Awake()
        {
            CacheRequiredComponents();
            _damagePresentationComponent?.EnsureSubscribed();
        }

        /// <summary>为未经过生成器初始化的场景实体提供默认生命值。</summary>
        protected virtual void Start()
        {
            // 覆盖容器注入晚于 OnEnable 的情况，确保首次施加 Buff 前已有中心目标句柄。
            GetBuffMotionEffects().RegisterTarget();
            if (MaxHealth <= 0f && enemyAttributes != null)
            {
                InitializeForSpawn(1f, 1f);
            }
        }

        /// <summary>启用时补齐必需组件的事件订阅和空间注册。</summary>
        protected virtual void OnEnable()
        {
            CacheRequiredComponents();
            GetBuffMotionEffects().ResetControlResistanceCache();
            _damagePresentationComponent?.EnsureSubscribed();
            GetBuffMotionEffects().RegisterTarget();
            GetBuffMotionEffects().RegisterSpatial();
            NotifyActivationReady();
        }

        /// <summary>池化或停用时释放 ECS 代理并注销 Buff 与空间目标。</summary>
        protected virtual void OnDisable()
        {
            _flockBridge?.Runtime?.ReleaseAgent(_flockBridge);
            GetBuffMotionEffects().UnregisterTarget();
            GetBuffMotionEffects().UnregisterSpatial();
        }

        /// <summary>在选中实体时绘制调试范围。</summary>
        protected virtual void OnDrawGizmosSelected()
        {
            if (!showDebugInfo)
            {
                return;
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 1f);
        }

        /// <summary>初始化本次生成的属性、分类和速度状态。</summary>
        /// <param name="healthMultiplier">生命倍率。</param>
        /// <param name="speedMultiplier">速度倍率。</param>
        /// <param name="spawnCategory">本次生成分类。</param>
        public virtual void InitializeForSpawn(
            float healthMultiplier,
            float speedMultiplier,
            EnemySpawnCategory spawnCategory = EnemySpawnCategory.Normal)
        {
            if (enemyAttributes == null)
            {
                Debug.LogError($"[Enemy] {name}: EnemyAttributesSO 未配置!", this);
                return;
            }

            CacheRequiredComponents();
            _spawnCategory = spawnCategory;
            _healthComponent?.InitializeForSpawn(healthMultiplier);
            SpeedMultiplier = speedMultiplier;
            GetBuffMotionEffects().SyncMovementSpeed();
            GetBuffMotionEffects().RegisterSpatial();
            NotifyActivationReady();
        }

        /// <summary>向生命组件提交伤害请求并发布全局实际扣血事件。</summary>
        /// <param name="request">伤害请求。</param>
        /// <returns>伤害处理结果。</returns>
        public virtual DamageResult ReceiveDamageRequest(in DamageRequest request)
        {
            CacheRequiredComponents();
            NotifyActivationReady();
            if (_healthComponent == null)
            {
                return new DamageResult
                {
                    Outcome = DamageOutcome.Invalid
                };
            }

            DamageResult result = _healthComponent.ReceiveDamageRequest(request);
            if (result.WasApplied)
            {
                AnyDamageApplied?.Invoke(this, result);
            }

            return result;
        }

        /// <summary>注册一个不重复的伤害前置拦截器。</summary>
        /// <param name="interceptor">要注册的拦截器。</param>
        public void RegisterPreDamageInterceptor(EnemyDamageInterceptor interceptor)
        {
            CacheRequiredComponents();
            _healthComponent?.RegisterInterceptor(interceptor);
        }

        /// <summary>移除一个伤害前置拦截器。</summary>
        /// <param name="interceptor">要移除的拦截器。</param>
        public void UnregisterPreDamageInterceptor(EnemyDamageInterceptor interceptor)
        {
            _healthComponent?.UnregisterInterceptor(interceptor);
        }

        /// <summary>直接请求完整死亡流程，不经过伤害拦截器。</summary>
        public void Kill() => DieFromHealth(EnemyDeathCause.Forced);

        /// <summary>以吞噬原因结束敌人生命，不产生奖励。</summary>
        public void KillAsConsumed() => DieFromHealth(EnemyDeathCause.Consumed);

        /// <summary>对象池租借时清除实体上一次租借的状态。</summary>
        public void OnRentFromPool() => ResetForReuse();

        /// <summary>对象池归还时清除实体状态。</summary>
        public void OnReturnToPool() => ResetForReuse();

        /// <summary>将实体重置为可重新初始化的状态。</summary>
        public virtual void ResetForReuse()
        {
            GetBuffMotionEffects().UnregisterSpatial();
            CacheRequiredComponents();
            // 死亡路径会刻意保持源 Renderer 隐藏；新一轮租借前恢复可见，等待 ECS 重新接管。
            _flockBridge?.SetEcsPresentationEnabled(false);
            _isDead = false;
            DeathCause = EnemyDeathCause.None;
            _spawnCategory = EnemySpawnCategory.Normal;
            GetBuffMotionEffects().ResetControlResistanceCache();
            if (_enemyCollider != null)
            {
                _enemyCollider.enabled = true;
            }

            GetBuffMotionEffects().ResetForReuse();
            _healthComponent?.ResetForReuse();
            SpeedMultiplier = 1f;
            GetBuffMotionEffects().SyncMovementSpeed();
            ClearDamagePresentationState();
            ReuseRequested?.Invoke();
        }

        /// <summary>返回当前承伤 Buff 倍率；该修正独立于伤害抗性。</summary>
        /// <returns>当前伤害倍率。</returns>
        internal float GetDamageTakenMultiplier() =>
            GetBuffMotionEffects().GetDamageTakenMultiplier();
        /// <summary>返回攻击 Buff 合成后的敌方输出伤害倍率。</summary>
        internal float GetAttackDamageMultiplier() =>
            GetBuffMotionEffects().GetAttackDamageMultiplier();
        /// <summary>返回限制敌方发起新攻击的状态倍率。</summary>
        internal float GetAttackAllowedMultiplier() =>
            GetBuffMotionEffects().GetAttackAllowedMultiplier();
        /// <summary>通知表现组件和兼容子类已经实际扣除生命。</summary>
        /// <param name="damageAmount">实际扣除的伤害量。</param>
        internal void NotifyDamageTaken(float damageAmount)
        {
            DamageTaken?.Invoke(this, damageAmount);
            OnDamageTaken(damageAmount);
        }

        /// <summary>捕获死亡攻击倍率、结束战斗生命周期并保留 ECS 死亡表现。</summary>
        internal void DieFromHealth(EnemyDeathCause cause = EnemyDeathCause.Damage)
        {
            if (_isDead)
            {
                return;
            }

            float deathAttackDamageMultiplier = GetAttackDamageMultiplier();
            _healthComponent?.SetHealthToZero(deathAttackDamageMultiplier);
            DeathCause = cause;
            _isDead = true;
            // 死亡实体不再参与任何 Buff 调度，连同永久运动效果一起注销。
            GetBuffMotionEffects().UnregisterTarget();
            // ECS 实体必须保留到 EnemySpawner 回收，才能完成溶解进度。
            GetBuffMotionEffects().UnregisterSpatial();
            DeathStarted?.Invoke(this);

            if (_enemyCollider != null)
            {
                _enemyCollider.enabled = false;
            }

            OnDeath();
            OnDied?.Invoke(this);
        }

        /// <summary>兼容集中视觉系统调用；表现状态不再缓存在敌人宿主。</summary>
        /// <param name="color">当前表现颜色。</param>
        /// <param name="timer">当前表现剩余时间。</param>
        internal void SetDamagePresentationState(Color color, float timer) { }

        /// <summary>兼容集中视觉系统重置调用；敌人宿主不保存表现镜像。</summary>
        internal void ClearDamagePresentationState() { }

        /// <summary>缓存实体运行所需组件，仅为临时对象补齐物理碰撞体。</summary>
        private void CacheRequiredComponents()
        {
            if (_lockAnchorRenderers == null)
            {
                _lockAnchorRenderers = GetComponentsInChildren<Renderer>(true);
            }

            if (_enemyCollider == null && !TryGetComponent(out _enemyCollider))
            {
                _enemyCollider = gameObject.AddComponent<BoxCollider>();
            }

            if (_healthComponent == null)
            {
                TryGetComponent(out _healthComponent);
            }

            if (_flockBridge == null)
            {
                TryGetComponent(out _flockBridge);
            }

            if (_damagePresentationComponent == null)
            {
                TryGetComponent(out _damagePresentationComponent);
            }
        }

        /// <summary>取得敌人的 Buff 与移动效果协作者。</summary>
        private EnemyBuffMotionEffects GetBuffMotionEffects() =>
            _buffMotionEffects ??= new EnemyBuffMotionEffects(
                this,
                () => _buffService,
                () => _flockBridge,
                () => SpeedMultiplier,
                () => _spatialService,
                slot => SpatialSlot = slot,
                CacheRequiredComponents);

        /// <summary>通知缓存的已挂载机制完成一次宿主激活初始化。</summary>
        private void NotifyActivationReady()
        {
            GetBuffMotionEffects().RegisterTarget();
            _activationNotifier ??= new EnemyActivationNotifier(gameObject);
            _activationNotifier.Notify();
            ActivationReady?.Invoke();
        }

        /// <summary>受击后的兼容扩展回调。</summary>
        /// <param name="damageAmount">实际扣除的伤害量。</param>
        protected virtual void OnDamageTaken(float damageAmount)
        {
        }

        /// <summary>死亡后的兼容扩展回调。</summary>
        protected virtual void OnDeath()
        {
        }

        #region IDamageable
        Vector3 IDamageable.Position => transform.position;
        bool IDamageable.IsAlive => !_isDead && gameObject.activeInHierarchy;
        Transform IDamageable.Transform => transform;
        CombatTargetKind ICombatTarget.TargetKind => CombatTargetKind.Enemy;
        float IHealable.Heal(float amount)
        {
            CacheRequiredComponents();
            return _healthComponent != null ? _healthComponent.Heal(amount) : 0f;
        }
        bool IControlResistant.CanReceiveHarmfulEffects
            => GetBuffMotionEffects().CanReceiveHarmfulEffects;

        #endregion
        #region ILockable
        float ILockable.ThreatLevel => Mathf.Clamp(
            (_spawnCategory == EnemySpawnCategory.Boss
                ? enemyAttributes.bossThreatLevel
                : EnemyType switch { EnemyType.Tank => 60f, EnemyType.Fast => 40f, _ => 30f }) *
            (0.5f + 0.5f * (MaxHealth > 0f ? CurrentHealth / MaxHealth : 1f)),
            0f,
            100f);

        bool ILockable.IsLockable => !_isDead && gameObject.activeInHierarchy;
        Vector3 ILockable.LockAnchorPoint => GetLockAnchorPoint();
        TargetCategory ILockable.Category => _spawnCategory == EnemySpawnCategory.Boss
            ? TargetCategory.Boss
            : TargetCategory.Normal;
        float ILockable.HealthPercent => HealthPercent;

        /// <summary>将锁定锚点放在可见模型边界沿局部上方向的顶部。</summary>
        private Vector3 GetLockAnchorPoint()
        {
            Bounds bounds = GetRenderBounds();
            Vector3 up = transform.up;
            Vector3 extents = bounds.extents;
            float projectedExtent =
                Mathf.Abs(up.x) * extents.x +
                Mathf.Abs(up.y) * extents.y +
                Mathf.Abs(up.z) * extents.z;

            return bounds.center + up * (projectedExtent + LockAnchorPadding);
        }

        /// <summary>组合模型 Renderer 的世界边界，无模型时回退到实体碰撞体。</summary>
        private Bounds GetRenderBounds()
        {
            bool hasBounds = false;
            Bounds bounds = default;
            for (int index = 0; index < _lockAnchorRenderers.Length; index++)
            {
                Renderer renderer = _lockAnchorRenderers[index];
                if (renderer == null || !renderer.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            if (hasBounds)
            {
                return bounds;
            }

            return _enemyCollider != null
                ? _enemyCollider.bounds
                : new Bounds(transform.position, Vector3.zero);
        }

        #endregion
        #region IEnemyMotionEffectTarget
        /// <summary>向 ECS 代理提交速度倍率，不暴露 ECS Entity。</summary>
        void IEnemyMotionEffectTarget.SetMovementSpeedMultiplier(float multiplier)
        {
            CacheRequiredComponents();
            GetBuffMotionEffects().SetMovementSpeedMultiplier(multiplier);
        }

        /// <summary>向 ECS 代理提交击退冲量；未绑定 ECS 时直接移动托管对象。</summary>
        void IEnemyMotionEffectTarget.AddExternalImpulse(Vector3 impulse)
        {
            GetBuffMotionEffects().AddExternalImpulse(impulse);
        }

        /// <summary>保存指定来源的永久速度倍率，并立即同步 ECS 运动代理。</summary>
        void IPermanentEnemyMotionEffectTarget.SetPermanentMovementSpeedMultiplier(
            float multiplier,
            int sourceInstanceId) =>
            GetBuffMotionEffects().SetPermanentMovementSpeedMultiplier(multiplier, sourceInstanceId);

        /// <summary>移除指定来源的永久速度倍率，并恢复剩余状态合成结果。</summary>
        void IPermanentEnemyMotionEffectTarget.ClearPermanentMovementSpeedMultiplier(int sourceInstanceId) =>
            GetBuffMotionEffects().ClearPermanentMovementSpeedMultiplier(sourceInstanceId);

        #endregion
        #region IBuffReceiver

        /// <summary>以默认来源应用 Buff。</summary>
        /// <param name="definition">Buff 静态定义。</param>
        public void ApplyBuff(BuffDefinitionSO definition) => ApplyBuff(definition, 0);

        /// <summary>以指定来源应用 Buff。</summary>
        /// <param name="definition">Buff 静态定义。</param>
        /// <param name="sourceInstanceId">Buff 来源实例标识。</param>
        public void ApplyBuff(BuffDefinitionSO definition, int sourceInstanceId) =>
            GetBuffMotionEffects().ApplyBuff(definition, sourceInstanceId);

        /// <summary>
        /// 仅在目标没有同类活动 Buff 时施加一个 Buff。
        /// </summary>
        /// <param name="definition">要施加的 Buff 静态定义。</param>
        /// <param name="sourceInstanceId">Buff 来源实例标识。</param>
        /// <returns>本次实际新增 Buff 时返回 true。</returns>
        public bool TryApplyBuffIfAbsent(BuffDefinitionSO definition, int sourceInstanceId) =>
            GetBuffMotionEffects().TryApplyBuffIfAbsent(definition, sourceInstanceId);

        /// <summary>报告当前目标状态给场景级 BuffService。</summary>
        bool IBuffTarget.IsActive => isActiveAndEnabled && gameObject.activeInHierarchy;
        bool IBuffTarget.IsAlive => !_isDead && gameObject.activeInHierarchy;
        bool IBuffTarget.CanReceiveHarmfulEffects =>
            ((IControlResistant)this).CanReceiveHarmfulEffects;

        /// <summary>Buff 属性变化时同步需要主动提交的移动状态。</summary>
        void IBuffTarget.OnBuffModifiersChanged(BuffModifierType modifierType) =>
            GetBuffMotionEffects().OnBuffModifiersChanged(modifierType);

        /// <summary>吸收另一个敌人的当前生命并结束对方生命。</summary>
        public bool TryAbsorb(Enemy target)
        {
            if (target == null || target == this || _isDead || target.IsDead ||
                target.CurrentHealth <= 0f ||
                target.SpawnCategory is not (EnemySpawnCategory.Normal or EnemySpawnCategory.Child))
            {
                return false;
            }

            float absorbedHealth = target.CurrentHealth;
            target.KillAsConsumed();
            CacheRequiredComponents();
            _healthComponent?.AbsorbHealth(absorbedHealth);
            return true;
        }

        #endregion
    }
}
