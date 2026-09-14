using Services;
using UnityEngine;
using Utils;
using VContainer;

namespace EnemySystem.Components
{
    /// <summary>
    /// 坦克敌人的护盾行为。
    /// <para>护盾参数由所属 <see cref="EnemyAttributesSO"/> 提供，本组件只保存当前护盾值并执行恢复。</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    public class EnemyShieldComponent : MonoBehaviour, IPooledObject, IEnemyActivationListener
    {
        [Inject] private IEffectService _effectService;

        private Enemy _enemy;
        private float _shieldValue;
        private float _shieldRegenRate;
        private float _shieldRegenDelay;
        private float _currentShield;
        private float _lastDamageTime;
        private bool _subscribed;
        private bool _activationSubscribed;
        private bool _initialized;

        /// <summary>当前是否仍有可吸收伤害的护盾。</summary>
        public bool IsShieldActive => _currentShield > 0f;

        /// <summary>缓存敌人并读取实体配置。</summary>
        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
            InitializeState();
            SubscribeActivation();
        }

        /// <summary>刷新配置、重置护盾并注册伤害前置拦截器。</summary>
        private void OnEnable()
        {
            if (_enemy == null)
            {
                _enemy = GetComponent<Enemy>();
            }

            RefreshConfiguration();
            ResetShield();
            _initialized = true;
            if (_enemy != null)
            {
                SubscribeActivation();
            }
            RegisterInterceptor();
        }

        /// <summary>确保护盾在未执行 Unity 生命周期时也能参与伤害处理。</summary>
        internal void EnsureRegistered()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (_enemy == null)
            {
                _enemy = GetComponent<Enemy>();
            }

            RefreshConfiguration();
            if (!_initialized || !_subscribed)
            {
                ResetShield();
            }

            _initialized = true;
            RegisterInterceptor();
        }

        /// <summary>注销伤害前置拦截器，避免对象池实例重复订阅。</summary>
        private void OnDisable()
        {
            if (_enemy != null && _subscribed)
            {
                _enemy.UnregisterPreDamageInterceptor(HandlePreDamage);
                _subscribed = false;
            }

            if (_enemy != null)
            {
                UnsubscribeActivation();
            }
        }

        /// <summary>拦截伤害并优先扣除当前护盾。</summary>
        /// <param name="enemy">收到伤害的敌人。</param>
        /// <param name="originalDamage">尚未修正的伤害量。</param>
        /// <param name="finalDamage">输出的剩余伤害量。</param>
        /// <returns>总是继续伤害流程；护盾不足时剩余伤害继续扣除生命。</returns>
        private bool HandlePreDamage(Enemy enemy, float originalDamage, ref float finalDamage)
        {
            if (_currentShield <= 0f)
            {
                return true;
            }

            _lastDamageTime = Time.time;
            float absorb = Mathf.Min(_currentShield, finalDamage);
            _currentShield -= absorb;
            finalDamage -= absorb;

            _effectService?.Play(EffectSystem.EffectId.ShieldHit, transform.position);
            return true;
        }

        /// <summary>在等待恢复时间结束后逐帧恢复护盾。</summary>
        private void Update()
        {
            if (_currentShield >= _shieldValue || Time.time - _lastDamageTime < _shieldRegenDelay)
            {
                return;
            }

            _currentShield = Mathf.Min(
                _shieldValue,
                _currentShield + _shieldRegenRate * Time.deltaTime);
        }

        /// <summary>对象池租借时读取最新配置并恢复满盾状态。</summary>
        public void OnRentFromPool()
        {
            if (_enemy == null)
            {
                _enemy = GetComponent<Enemy>();
            }

            RefreshConfiguration();
            ResetShield();
            _initialized = true;
        }

        /// <summary>对象池归还时清空运行时护盾状态。</summary>
        public void OnReturnToPool()
        {
            _currentShield = 0f;
            _lastDamageTime = 0f;
            _initialized = false;
        }

        /// <summary>读取配置并初始化护盾运行时状态。</summary>
        private void InitializeState()
        {
            RefreshConfiguration();
            ResetShield();
            _initialized = true;
        }

        /// <summary>注册护盾伤害拦截器且避免重复注册。</summary>
        private void RegisterInterceptor()
        {
            if (_enemy != null && !_subscribed)
            {
                _enemy.RegisterPreDamageInterceptor(HandlePreDamage);
                _subscribed = true;
            }
        }

        /// <summary>从敌人实体配置刷新护盾运行时参数。</summary>
        private void RefreshConfiguration()
        {
            EnemyAttributesSO attributes = _enemy != null ? _enemy.Attributes : null;
            _shieldValue = attributes != null ? Mathf.Max(0f, attributes.shieldValue) : 0f;
            _shieldRegenRate = attributes != null ? Mathf.Max(0f, attributes.shieldRegenRate) : 0f;
            _shieldRegenDelay = attributes != null ? Mathf.Max(0f, attributes.shieldRegenDelay) : 0f;
        }

        /// <summary>将护盾恢复到配置上限并重置受击计时。</summary>
        private void ResetShield()
        {
            _currentShield = _shieldValue;
            _lastDamageTime = -_shieldRegenDelay;
        }

        /// <summary>实体主动重置时恢复护盾，支持非禁用状态下的复用。</summary>
        private void HandleReuseRequested()
        {
            _subscribed = false;
            RefreshConfiguration();
            ResetShield();
            _initialized = true;
            RegisterInterceptor();
        }

        /// <summary>订阅宿主完成启用后的初始化通知。</summary>
        private void SubscribeActivation()
        {
            if (_enemy != null && !_activationSubscribed)
            {
                _enemy.ReuseRequested += HandleReuseRequested;
                _enemy.ActivationReady += HandleActivationReady;
                _activationSubscribed = true;
            }
        }

        /// <summary>实体组件全部启用后刷新护盾并注册拦截器。</summary>
        private void HandleActivationReady()
        {
            RefreshConfiguration();
            if (!_initialized || !_subscribed)
            {
                ResetShield();
            }

            _initialized = true;
            RegisterInterceptor();
        }

        /// <summary>响应宿主广播，兼容组件在 inactive 对象上延迟挂载的场景。</summary>
        void IEnemyActivationListener.OnEnemyActivationReady()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            if (_enemy == null)
            {
                _enemy = GetComponent<Enemy>();
            }

            SubscribeActivation();
            HandleActivationReady();
        }

        /// <summary>注销宿主生命周期通知。</summary>
        private void UnsubscribeActivation()
        {
            if (_enemy != null && _activationSubscribed)
            {
                _enemy.ReuseRequested -= HandleReuseRequested;
                _enemy.ActivationReady -= HandleActivationReady;
                _activationSubscribed = false;
            }
        }
    }
}
