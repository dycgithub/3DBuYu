using EnemySystem.Visual;
using FlockingSystem.ECS;
using UnityEngine;
using UnityEngine.Serialization;
using Utils;
using VContainer;

namespace EnemySystem.Components
{
    /// <summary>
    /// 敌人与集中视觉系统之间的生命周期绑定。
    /// <para>组件不再逐敌人更新材质、不创建特效；ECS 实例属性和特效由 EnemyVisualSystem 统一驱动。</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    public sealed class EnemyDamagePresentationComponent : MonoBehaviour, IPooledObject, IEnemyActivationListener
    {
        [Header("命中表现")]
        [FormerlySerializedAs("damageFlashColor")]
        [SerializeField] private Color damageFlashColor = Color.white;
        [FormerlySerializedAs("damageFlashDuration")]
        [Min(0f)] [SerializeField] private float damageFlashDuration = 0.08f;

        [Header("死亡表现")]
        [FormerlySerializedAs("deathHighlightColor")]
        [SerializeField] private Color deathHighlightColor = new(1f, 0.75f, 0.2f, 1f);
        [FormerlySerializedAs("deathHighlightDuration")]
        [Min(0f)] [SerializeField] private float deathHighlightDuration = 0.18f;

        [Inject] private EnemyVisualSystem _visualSystem;

        private Enemy _enemy;
        private EnemyFlockBridge _flockBridge;
        private bool _subscribed;

        /// <summary>组件配置的受击闪白颜色，供旧调试镜像读取。</summary>
        internal Color DamageFlashColor => damageFlashColor;

        /// <summary>组件配置的受击闪白时长。</summary>
        internal float DamageFlashDuration => Mathf.Max(0f, damageFlashDuration);

        /// <summary>组件配置的死亡高亮颜色，供旧调试镜像读取。</summary>
        internal Color DeathHighlightColor => deathHighlightColor;

        /// <summary>组件配置的死亡溶解时长，供生成器决定回收时机。</summary>
        internal float DeathHighlightDuration
        {
            get
            {
                float configuredDuration = _enemy != null && _enemy.Attributes != null &&
                    _enemy.Attributes.visualConfig != null
                    ? _enemy.Attributes.visualConfig.deathDuration
                    : deathHighlightDuration;
                return Mathf.Max(0f, configuredDuration);
            }
        }

        /// <summary>缓存实体和 ECS 桥接。</summary>
        private void Awake()
        {
            CacheComponents();
        }

        /// <summary>启用时补齐绑定和生命周期订阅。</summary>
        private void OnEnable()
        {
            CacheComponents();
            EnsureSubscribed();
        }

        /// <summary>在宿主先于本组件完成激活时补做视觉系统绑定。</summary>
        internal void EnsureSubscribed()
        {
            CacheComponents();
            if (_enemy == null)
            {
                return;
            }

            _visualSystem?.Register(_enemy, _flockBridge, this);
            if (_subscribed)
            {
                return;
            }

            _enemy.DeathStarted += HandleDeathStarted;
            _enemy.ReuseRequested += HandleReuseRequested;
            _subscribed = true;
        }

        /// <summary>注销事件并释放集中视觉系统中的实例引用。</summary>
        private void OnDisable()
        {
            if (_enemy != null && _subscribed)
            {
                _enemy.DeathStarted -= HandleDeathStarted;
                _enemy.ReuseRequested -= HandleReuseRequested;
                _subscribed = false;
            }

            _visualSystem?.Unregister(_enemy);
            _enemy?.ClearDamagePresentationState();
        }

        /// <summary>对象池租借时重置集中视觉状态。</summary>
        public void OnRentFromPool()
        {
            _visualSystem?.Reset(_enemy);
            _enemy?.ClearDamagePresentationState();
        }

        /// <summary>对象池归还时重置集中视觉状态。</summary>
        public void OnReturnToPool()
        {
            _visualSystem?.Reset(_enemy);
            _enemy?.ClearDamagePresentationState();
        }

        /// <summary>接收敌人死亡生命周期并启动溶解流程。</summary>
        private void HandleDeathStarted(Enemy enemy)
        {
            _visualSystem?.BeginDeath(enemy);
        }

        /// <summary>接收对象池复用通知并清除旧视觉状态。</summary>
        private void HandleReuseRequested()
        {
            _visualSystem?.Reset(_enemy);
            _enemy?.ClearDamagePresentationState();
        }

        /// <summary>由 Enemy 宿主在所有组件启用后补做绑定。</summary>
        void IEnemyActivationListener.OnEnemyActivationReady()
        {
            EnsureSubscribed();
        }

        /// <summary>缓存宿主和 ECS 桥接引用。</summary>
        private void CacheComponents()
        {
            _enemy ??= GetComponent<Enemy>();
            _flockBridge ??= GetComponent<EnemyFlockBridge>();
        }
    }
}
