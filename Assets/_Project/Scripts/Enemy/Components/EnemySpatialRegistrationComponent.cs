using FlockingSystem.ECS;
using Services;
using SpatialSystem.Bridge;
using UnityEngine;
using Utils;
using VContainer;

namespace EnemySystem.Components
{
    /// <summary>
    /// 敌人的空间查询注册组件。
    /// <para>组件负责空间注册、注销、位置同步和现有 ECS 桥接的速度倍率同步。</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    public sealed class EnemySpatialRegistrationComponent : MonoBehaviour, IPooledObject
    {
        [Inject] private ISpatialQueryService _spatialService;
        [Inject] private EnemyFlockSettingsSO _flockSettings;

        private Enemy _enemy;
        private EnemyFlockBridge _flockBridge;
        private int _spatialEntityId = -1;
        private bool _lifecycleSubscribed;

        /// <summary>缓存实体和 ECS 桥接。</summary>
        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
            _flockBridge = GetComponent<EnemyFlockBridge>();
        }

        /// <summary>启用时按配置注册空间实体。</summary>
        private void OnEnable()
        {
            if (_enemy == null)
            {
                _enemy = GetComponent<Enemy>();
            }

            if (_flockBridge == null)
            {
                _flockBridge = GetComponent<EnemyFlockBridge>();
            }

            SubscribeLifecycle();
            RegisterSpatialEntity();
        }

        /// <summary>禁用时注销事件和空间实体，避免对象池残留。</summary>
        private void OnDisable()
        {
            UnsubscribeLifecycle();
            UnregisterSpatialEntity();
        }

        /// <summary>销毁时再次保证事件和空间实体已经注销。</summary>
        private void OnDestroy()
        {
            UnsubscribeLifecycle();
            UnregisterSpatialEntity();
        }

        /// <summary>同步 ECS 速度倍率和空间查询位置。</summary>
        private void Update()
        {
            if (_enemy == null || _enemy.IsDead)
            {
                return;
            }

            if (_flockBridge != null && _flockBridge.IsEcsControlled)
            {
                _flockBridge.SetSpeedMultiplier(_enemy.GetMovementSpeedMultiplier());
            }

            if (_spatialEntityId >= 0)
            {
                ISpatialQueryService spatialService = ResolveSpatialService();
                spatialService?.UpdatePosition(_spatialEntityId, transform.position);
            }
        }

        /// <summary>在对象池租借前清理旧的空间注册状态。</summary>
        public void OnRentFromPool()
        {
            UnregisterSpatialEntity();
        }

        /// <summary>在对象池归还前清理空间注册状态。</summary>
        public void OnReturnToPool()
        {
            UnregisterSpatialEntity();
        }

        /// <summary>为编辑器测试和注入延迟场景补做一次注册。</summary>
        internal void EnsureRegistered()
        {
            SubscribeLifecycle();
            RegisterSpatialEntity();
        }

        /// <summary>由宿主转发禁用生命周期，兼容编辑器模式下组件回调不完整的场景。</summary>
        internal void EnsureUnregistered()
        {
            UnregisterSpatialEntity();
        }

        /// <summary>按敌人类型读取邻居半径并执行一次注册。</summary>
        private void RegisterSpatialEntity()
        {
            if (_enemy == null)
            {
                _enemy = GetComponent<Enemy>();
            }

            if (!isActiveAndEnabled ||
                _spatialEntityId >= 0 ||
                _enemy == null ||
                !_enemy.HasConfiguration)
            {
                return;
            }

            ISpatialQueryService spatialService = ResolveSpatialService();
            EnemyFlockSettingsSO flockSettings = ResolveFlockSettings();
            if (spatialService == null || flockSettings == null)
            {
                return;
            }

            float neighbourDistance = flockSettings
                .GetProfileForEnemyType(_enemy.EnemyType, 1f)
                .NeighbourDistance;
            _spatialEntityId = spatialService.Register(
                _enemy,
                neighbourDistance,
                SpatialRegistry.LAYER_ENEMY);
        }

        /// <summary>注销已登记的空间实体并清空本地标识。</summary>
        private void UnregisterSpatialEntity()
        {
            ISpatialQueryService spatialService = ResolveSpatialService();
            if (spatialService != null && _spatialEntityId >= 0)
            {
                spatialService.Unregister(_spatialEntityId);
            }

            _spatialEntityId = -1;
        }

        private void SubscribeLifecycle()
        {
            if (_enemy != null && !_lifecycleSubscribed)
            {
                _enemy.DeathStarted += HandleDeathStarted;
                _enemy.ReuseRequested += HandleReuseRequested;
                _lifecycleSubscribed = true;
            }
        }

        private void UnsubscribeLifecycle()
        {
            if (_enemy != null && _lifecycleSubscribed)
            {
                _enemy.DeathStarted -= HandleDeathStarted;
                _enemy.ReuseRequested -= HandleReuseRequested;
                _lifecycleSubscribed = false;
            }
        }

        private void HandleDeathStarted(Enemy enemy)
        {
            UnregisterSpatialEntity();
        }

        private void HandleReuseRequested()
        {
            RegisterSpatialEntity();
        }

        /// <summary>优先使用组件注入的空间服务，再兼容宿主注入入口。</summary>
        private ISpatialQueryService ResolveSpatialService()
        {
            return _spatialService ?? _enemy?.SpatialService;
        }

        /// <summary>优先使用组件注入的群游配置，再兼容宿主注入入口。</summary>
        private EnemyFlockSettingsSO ResolveFlockSettings()
        {
            return _flockSettings ?? _enemy?.FlockSettings;
        }
    }
}
