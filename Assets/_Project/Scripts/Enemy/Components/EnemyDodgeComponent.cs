using EffectSystem;
using Services;
using UnityEngine;
using VContainer;

namespace EnemySystem.Components
{
    /// <summary>
    /// 快速敌人的闪避行为。
    /// <para>闪避数值由所属 <see cref="EnemyAttributesSO"/> 提供，本组件只负责拦截伤害和执行位移。</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    public class EnemyDodgeComponent : MonoBehaviour, IEnemyActivationListener
    {
        [Inject] private IEffectService _effectService;

        private Enemy _enemy;
        private float _dodgeChance;
        private float _dodgeDistance;
        private bool _subscribed;
        private bool _activationSubscribed;

        /// <summary>缓存敌人并读取实体配置。</summary>
        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
            RefreshConfiguration();
            SubscribeActivation();
        }

        /// <summary>读取配置并注册伤害前置拦截器。</summary>
        private void OnEnable()
        {
            if (_enemy == null)
            {
                _enemy = GetComponent<Enemy>();
            }

            RefreshConfiguration();
            if (_enemy != null)
            {
                SubscribeActivation();
            }
            RegisterInterceptor();
        }

        /// <summary>确保闪避在未执行 Unity 生命周期时也能参与伤害处理。</summary>
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

        /// <summary>
        /// 判断当前伤害是否被闪避。
        /// </summary>
        /// <param name="enemy">收到伤害的敌人。</param>
        /// <param name="originalDamage">尚未修正的伤害量。</param>
        /// <param name="finalDamage">输出的最终伤害量；闪避时不会使用该值。</param>
        /// <returns>返回 true 继续伤害流程，返回 false 表示完全闪避。</returns>
        private bool HandlePreDamage(Enemy enemy, float originalDamage, ref float finalDamage)
        {
            if (_dodgeChance <= 0f || Random.value >= _dodgeChance)
            {
                return true;
            }

            PerformDodge();
            return false;
        }

        /// <summary>执行水平随机位移并播放闪避效果。</summary>
        private void PerformDodge()
        {
            Vector3 direction = Random.insideUnitSphere;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
            {
                direction = transform.forward;
            }

            direction.Normalize();
            transform.position += direction * _dodgeDistance;
            _effectService?.Play(EffectId.EnemyDodge, transform.position);
        }

        /// <summary>注册闪避伤害拦截器且避免重复注册。</summary>
        private void RegisterInterceptor()
        {
            if (_enemy != null && !_subscribed)
            {
                _enemy.RegisterPreDamageInterceptor(HandlePreDamage);
                _subscribed = true;
            }
        }

        /// <summary>实体主动重置时重新挂载闪避拦截器。</summary>
        private void HandleReuseRequested()
        {
            _subscribed = false;
            RefreshConfiguration();
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

        /// <summary>实体组件全部启用后刷新闪避并注册拦截器。</summary>
        private void HandleActivationReady()
        {
            RefreshConfiguration();
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

        /// <summary>从敌人实体配置刷新本组件的运行时只读数据。</summary>
        private void RefreshConfiguration()
        {
            EnemyAttributesSO attributes = _enemy != null ? _enemy.Attributes : null;
            _dodgeChance = attributes != null ? Mathf.Clamp01(attributes.dodgeChance) : 0f;
            _dodgeDistance = attributes != null ? Mathf.Max(0f, attributes.dodgeDistance) : 0f;
        }
    }
}
