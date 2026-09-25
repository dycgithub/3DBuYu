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
    /// <para>EnemyFlockBridge 是实体运行所需的桥接组件，由 RequireComponent 明确声明；Buff 状态由场景级 BuffManner 统一管理。</para>
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyFlockBridge))]
    [RequireComponent(typeof(EnemyHealthComponent))]
    [RequireComponent(typeof(EnemyDamagePresentationComponent))]
    public class Enemy : MonoBehaviour, ILockable, ICombatTarget, IHealable, IControlResistant, IBuffTarget, IBuffReceiver, IDamageRequestReceiver, IEnemyMotionEffectTarget, IPermanentEnemyMotionEffectTarget, IPooledObject
    {
        [FormerlySerializedAs("stats")]
        [FormerlySerializedAs("attributes")]
        [Header("实体配置")]
        [Tooltip("敌人实体配置(生命值/类型/行为参数)")]
        [SerializeField] private EnemyAttributesSO enemyAttributes;

        [Header("调试")]
        [SerializeField] private bool showDebugInfo;

        // 这些字段仅保留给现有编辑器测试和调试读取，真实表现状态由 EnemyDamagePresentationComponent 持有。
        private float _damageFlashTimer;
        private Color _activeFlashColor;

        private bool _isDead;
        private EnemyHealthComponent _healthComponent;
        private EnemyFlockBridge _flockBridge;
        private EnemyDamagePresentationComponent _damagePresentationComponent;
        private Collider _enemyCollider;
        private EnemyShieldComponent _shieldComponent;
        private bool _shieldLookupComplete;

        [Inject] private IBuffService _buffService;
        [Inject] private SpatialQueryService _spatialService;
        [Inject] private EnemyFlockSettingsSO _flockSettings;

        // 仅供编辑器旧测试在没有容器注入时使用；正式运行时始终由 GameLoop BuffManner 提供服务。
        private static readonly BuffManner EditorFallbackBuffManner = new();
        /// <summary>查询服务槽位；不参与 Flock slot 的分配和回收。</summary>
        internal int SpatialSlot { get; private set; } = -1;

        /// <summary>敌人设计编号。</summary>
        public int EnemyId => enemyAttributes != null ? Mathf.Max(1, enemyAttributes.enemyId) : 1;

        /// <summary>敌人类型。</summary>
        public EnemyType EnemyType => enemyAttributes != null
            ? enemyAttributes.enemyType
            : EnemyType.Normal;

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
        internal float GetMovementSpeedMultiplier()
        {
            IBuffService buffService = GetBuffService();
            float buffMultiplier = buffService != null
                ? buffService.GetModifierMultiplier(
                    this,
                    BuffModifierType.MovementSpeedMultiplier)
                : 1f;

            return Mathf.Max(0f, SpeedMultiplier) * Mathf.Max(0f, buffMultiplier);
        }

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
            RegisterBuffTarget();
            if (MaxHealth <= 0f && enemyAttributes != null)
            {
                InitializeForSpawn(1f, 1f);
            }
        }

        /// <summary>启用时补齐必需组件的事件订阅和空间注册。</summary>
        protected virtual void OnEnable()
        {
            CacheRequiredComponents();
            ResetShieldCache();
            _damagePresentationComponent?.EnsureSubscribed();
            RegisterBuffTarget();
            RegisterSpatial();
            NotifyActivationReady();
        }

        /// <summary>禁用时注销 Buff 目标并立即失效空间槽位。</summary>
        protected virtual void OnDisable()
        {
            // 任何非死亡路径的禁用也必须释放 ECS 实体，避免留下无效 SpatialSlot 绑定。
            _flockBridge?.Runtime?.ReleaseAgent(_flockBridge);
            UnregisterBuffTarget();
            UnregisterSpatial();
        }

        /// <summary>配置与注入就绪后登记宿主，位置由查询服务批量采样。</summary>
        private void RegisterSpatial()
        {
            if (SpatialSlot < 0 && isActiveAndEnabled && HasConfiguration && !_isDead && _spatialService != null)
            {
                SpatialSlot = _spatialService.RegisterEnemy(this);
                _flockBridge?.SyncSpatialSlot(SpatialSlot);
            }
        }

        /// <summary>回收、禁用和死亡共用的幂等注销入口。</summary>
        private void UnregisterSpatial()
        {
            int previousSlot = SpatialSlot;
            SpatialSlot = -1;
            _flockBridge?.SyncSpatialSlot(-1);
            _spatialService?.UnregisterEnemy(previousSlot);
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

        /// <summary>初始化本次生成的生命和速度参数。</summary>
        /// <param name="healthMultiplier">生命倍率。</param>
        /// <param name="speedMultiplier">速度倍率。</param>
        public virtual void InitializeForSpawn(float healthMultiplier, float speedMultiplier)
        {
            if (enemyAttributes == null)
            {
                Debug.LogError($"[Enemy] {name}: EnemyAttributesSO 未配置!", this);
                return;
            }

            CacheRequiredComponents();
            _healthComponent?.InitializeForSpawn(healthMultiplier);
            SpeedMultiplier = speedMultiplier;
            SyncMovementSpeed();
            RegisterSpatial();
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
        public void Kill()
        {
            DieFromHealth(EnemyDeathCause.Forced);
        }

        /// <summary>以吞噬原因结束敌人生命，不产生奖励。</summary>
        public void KillAsConsumed()
        {
            DieFromHealth(EnemyDeathCause.Consumed);
        }

        /// <summary>对象池租借时清除实体上一次租借的状态。</summary>
        public void OnRentFromPool()
        {
            ResetForReuse();
        }

        /// <summary>对象池归还时清除实体状态。</summary>
        public void OnReturnToPool()
        {
            ResetForReuse();
        }

        /// <summary>将实体重置为可重新初始化的状态。</summary>
        public virtual void ResetForReuse()
        {
            UnregisterSpatial();
            CacheRequiredComponents();
            // 死亡路径会刻意保持源 Renderer 隐藏；新一轮租借前恢复可见，等待 ECS 重新接管。
            _flockBridge?.SetEcsPresentationEnabled(false);
            _isDead = false;
            DeathCause = EnemyDeathCause.None;
            ResetShieldCache();
            if (_enemyCollider != null)
            {
                _enemyCollider.enabled = true;
            }

            IBuffService buffService = GetBuffService();
            buffService?.ClearAllBuffs(this);
            buffService?.ClearAllPermanentMovementSpeedMultipliers(this);
            UnregisterBuffTarget();
            _healthComponent?.ResetForReuse();
            SpeedMultiplier = 1f;
            SyncMovementSpeed();
            ClearDamagePresentationState();
            ReuseRequested?.Invoke();
        }

        /// <summary>返回当前承伤 Buff 倍率；该修正独立于伤害抗性。</summary>
        /// <returns>当前伤害倍率。</returns>
        internal float GetDamageTakenMultiplier()
        {
            IBuffService buffService = GetBuffService();
            if (buffService == null)
            {
                return 1f;
            }

            return buffService.GetModifierMultiplier(
                this,
                BuffModifierType.DamageTakenMultiplier);
        }

        /// <summary>返回攻击 Buff 合成后的敌方输出伤害倍率。</summary>
        internal float GetAttackDamageMultiplier()
        {
            return GetBuffService()?.GetModifierMultiplier(
                this,
                BuffModifierType.AttackDamage) ?? 1f;
        }

        /// <summary>返回限制敌方发起新攻击的状态倍率。</summary>
        internal float GetAttackAllowedMultiplier()
        {
            return GetBuffService()?.GetModifierMultiplier(
                this,
                BuffModifierType.AttackAvailability) ?? 1f;
        }

        /// <summary>通知表现组件和兼容子类已经实际扣除生命。</summary>
        /// <param name="damageAmount">实际扣除的伤害量。</param>
        internal void NotifyDamageTaken(float damageAmount)
        {
            DamageTaken?.Invoke(this, damageAmount);
            OnDamageTaken(damageAmount);
        }

        /// <summary>结束敌人的战斗生命周期，并保留 ECS 实体供死亡材质播放。</summary>
        internal void DieFromHealth(EnemyDeathCause cause = EnemyDeathCause.Damage)
        {
            if (_isDead)
            {
                return;
            }

            _healthComponent?.SetHealthToZero();
            DeathCause = cause;
            _isDead = true;
            // 死亡实体不再参与任何 Buff 调度，连同永久运动效果一起注销。
            UnregisterBuffTarget();
            // ECS 实体必须保留到 EnemySpawner 回收，才能完成溶解进度。
            UnregisterSpatial();
            DeathStarted?.Invoke(this);

            if (_enemyCollider != null)
            {
                _enemyCollider.enabled = false;
            }

            OnDeath();
            OnDied?.Invoke(this);
        }

        /// <summary>同步旧编辑器测试读取的表现状态镜像。</summary>
        /// <param name="color">当前表现颜色。</param>
        /// <param name="timer">当前表现剩余时间。</param>
        internal void SetDamagePresentationState(Color color, float timer)
        {
            _activeFlashColor = color;
            _damageFlashTimer = timer;
        }

        /// <summary>清除旧编辑器测试读取的表现状态镜像。</summary>
        internal void ClearDamagePresentationState()
        {
            _damageFlashTimer = 0f;
        }

        /// <summary>缓存实体运行所需组件，仅为临时对象补齐物理碰撞体。</summary>
        private void CacheRequiredComponents()
        {
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

        /// <summary>把生成倍率和 Buff 倍率提交给当前 ECS 运动代理。</summary>
        private void SyncMovementSpeed()
        {
            _flockBridge?.SetSpeedMultiplier(GetMovementSpeedMultiplier());
        }

        /// <summary>向中心 BuffManner 登记当前目标。</summary>
        private void RegisterBuffTarget()
        {
            GetBuffService()?.RegisterTarget(this);
        }

        /// <summary>从中心 BuffManner 注销当前目标。</summary>
        private void UnregisterBuffTarget()
        {
            GetBuffService()?.UnregisterTarget(this);
        }

        /// <summary>取得容器提供的场景 Buff 服务，编辑器旧测试使用共享回退实例。</summary>
        private IBuffService GetBuffService()
        {
            if (_buffService != null)
            {
                return _buffService;
            }

            return !Application.isPlaying ? EditorFallbackBuffManner : null;
        }

        /// <summary>通知所有已挂载机制完成一次宿主激活初始化。</summary>
        private void NotifyActivationReady()
        {
            RegisterBuffTarget();
            MonoBehaviour[] components = GetComponents<MonoBehaviour>();
            for (int index = 0; index < components.Length; index++)
            {
                if (components[index] is IEnemyActivationListener listener)
                {
                    listener.OnEnemyActivationReady();
                }
            }

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
        {
            get
            {
                if (!_shieldLookupComplete)
                {
                    TryGetComponent(out _shieldComponent);
                    _shieldLookupComplete = true;
                }

                return _shieldComponent == null || !_shieldComponent.IsShieldActive;
            }
        }

        #endregion

        #region ILockable

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

        bool ILockable.IsLockable => !_isDead && gameObject.activeInHierarchy;
        Vector3 ILockable.LockAnchorPoint => transform.position + Vector3.up * 2f;
        TargetCategory ILockable.Category => EnemyType == EnemyType.Tank
            ? TargetCategory.Boss
            : TargetCategory.Normal;
        float ILockable.HealthPercent => HealthPercent;

        #endregion

        #region IEnemyMotionEffectTarget

        /// <summary>向 ECS 代理提交速度倍率，不暴露 ECS Entity。</summary>
        void IEnemyMotionEffectTarget.SetMovementSpeedMultiplier(float multiplier)
        {
            CacheRequiredComponents();
            _flockBridge?.SetSpeedMultiplier(Mathf.Max(0f, multiplier));
        }

        /// <summary>向 ECS 代理提交击退冲量；未绑定 ECS 时直接移动托管对象。</summary>
        void IEnemyMotionEffectTarget.AddExternalImpulse(Vector3 impulse)
        {
            if (impulse.sqrMagnitude <= 0f)
            {
                return;
            }

            CacheRequiredComponents();
            if (_flockBridge != null && _flockBridge.IsEcsControlled)
            {
                _flockBridge.AddExternalImpulse(impulse);
                return;
            }

            transform.position += impulse;
        }

        /// <summary>保存指定来源的永久速度倍率，并立即同步 ECS 运动代理。</summary>
        void IPermanentEnemyMotionEffectTarget.SetPermanentMovementSpeedMultiplier(
            float multiplier,
            int sourceInstanceId)
        {
            GetBuffService()?.SetPermanentMovementSpeedMultiplier(
                this,
                multiplier,
                sourceInstanceId);
        }

        /// <summary>移除指定来源的永久速度倍率，并恢复剩余状态合成结果。</summary>
        void IPermanentEnemyMotionEffectTarget.ClearPermanentMovementSpeedMultiplier(int sourceInstanceId)
        {
            GetBuffService()?.ClearPermanentMovementSpeedMultiplier(this, sourceInstanceId);
        }

        #endregion

        #region IBuffReceiver

        /// <summary>以默认来源应用 Buff。</summary>
        /// <param name="definition">Buff 静态定义。</param>
        public void ApplyBuff(BuffDefinitionSO definition)
        {
            ApplyBuff(definition, 0);
        }

        /// <summary>以指定来源应用 Buff。</summary>
        /// <param name="definition">Buff 静态定义。</param>
        /// <param name="sourceInstanceId">Buff 来源实例标识。</param>
        public void ApplyBuff(BuffDefinitionSO definition, int sourceInstanceId)
        {
            if (definition == null)
            {
                return;
            }

            RegisterBuffTarget();
            GetBuffService()?.ApplyBuff(this, definition, sourceInstanceId);
        }

        /// <summary>
        /// 仅在目标没有同类活动 Buff 时施加一个 Buff。
        /// </summary>
        /// <param name="definition">要施加的 Buff 静态定义。</param>
        /// <param name="sourceInstanceId">Buff 来源实例标识。</param>
        /// <returns>本次实际新增 Buff 时返回 true。</returns>
        public bool TryApplyBuffIfAbsent(BuffDefinitionSO definition, int sourceInstanceId)
        {
            if (definition == null)
            {
                return false;
            }

            RegisterBuffTarget();
            IBuffService buffService = GetBuffService();
            return buffService != null &&
                buffService.TryApplyBuffIfAbsent(this, definition, sourceInstanceId);
        }

        /// <summary>报告当前目标状态给场景级 BuffManner。</summary>
        bool IBuffTarget.IsActive => isActiveAndEnabled && gameObject.activeInHierarchy;
        bool IBuffTarget.IsAlive => !_isDead && gameObject.activeInHierarchy;
        bool IBuffTarget.CanReceiveHarmfulEffects =>
            ((IControlResistant)this).CanReceiveHarmfulEffects;

        /// <summary>Buff 属性变化时同步需要主动提交的移动状态。</summary>
        void IBuffTarget.OnBuffModifiersChanged(BuffModifierType modifierType)
        {
            if (modifierType == BuffModifierType.MovementSpeedMultiplier)
            {
                SyncMovementSpeed();
            }
        }

        /// <summary>重置护盾组件缓存，使对象池复用时重新确认可选组件。</summary>
        private void ResetShieldCache()
        {
            _shieldComponent = null;
            _shieldLookupComplete = false;
        }

        /// <summary>吸收另一个敌人的当前生命并结束对方生命。</summary>
        public bool TryAbsorb(Enemy target)
        {
            if (target == null || target == this || _isDead || target.IsDead)
            {
                return false;
            }

            float absorbedHealth = target.CurrentHealth;
            if (absorbedHealth <= 0f)
            {
                return false;
            }

            target.KillAsConsumed();
            CacheRequiredComponents();
            _healthComponent?.AbsorbHealth(absorbedHealth);
            return true;
        }

        #endregion
    }
}
