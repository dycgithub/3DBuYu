using EnemySystem;
using Services;
using UnityEngine;
using VContainer;

namespace EnemySystem.Components
{
    /// <summary>
    /// 敌人死亡奖励组件。
    /// <para>奖励数值由所属 <see cref="EnemyAttributesSO"/> 提供；记录实际到账结果供死亡表现使用。</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    public sealed class EnemyDeathRewardComponent : MonoBehaviour, IEnemyActivationListener
    {
        [Inject] private IEnergyService _energy;
        [Inject] private IGameEventService _gameEventService;
        [Inject] private ICombatPhaseService _combatPhase;

        private Enemy _enemy;
        private bool _subscribed;
        private bool _rewarded;
        private bool _positiveRewardGranted;
        private bool _dependencyErrorReported;

        internal bool HasPositiveReward => _positiveRewardGranted;

        /// <summary>获取当前敌人配置中的能量奖励。</summary>
        public float EnergyReward
        {
            get
            {
                return _enemy != null && _enemy.Attributes != null
                    ? Mathf.Max(0f, _enemy.Attributes.energyReward)
                    : 0f;
            }
        }

        /// <summary>获取当前敌人配置中的基础积分奖励。</summary>
        public int PointsReward
        {
            get
            {
                return _enemy != null && _enemy.Attributes != null
                    ? Mathf.Max(0, _enemy.Attributes.pointsReward)
                    : 0;
            }
        }

        /// <summary>缓存所属敌人。</summary>
        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
        }

        /// <summary>重置奖励状态并注册死亡事件。</summary>
        private void OnEnable()
        {
            _rewarded = false;
            _positiveRewardGranted = false;
            _dependencyErrorReported = false;
            _enemy ??= GetComponent<Enemy>();

            if (_enemy != null && !_subscribed)
            {
                _enemy.OnDied += HandleDeath;
                _enemy.ReuseRequested += HandleReuseRequested;
                _subscribed = true;
            }
        }

        /// <summary>注销死亡事件，避免对象池实例重复提交奖励。</summary>
        private void OnDisable()
        {
            if (_enemy != null && _subscribed)
            {
                _enemy.OnDied -= HandleDeath;
                _enemy.ReuseRequested -= HandleReuseRequested;
                _subscribed = false;
            }
        }

        /// <summary>跳过非击杀类死亡，其余死亡按配置提交一次奖励。</summary>
        /// <param name="enemy">触发死亡事件的敌人。</param>
        private void HandleDeath(Enemy enemy)
        {
            if (_rewarded ||
                enemy == null ||
                enemy.DeathCause == EnemyDeathCause.Consumed ||
                enemy.DeathCause == EnemyDeathCause.PlayerContact ||
                enemy.DeathCause == EnemyDeathCause.ParentDied)
            {
                return;
            }

            if (enemy.Attributes == null)
            {
                Debug.LogError(
                    $"[EnemyDeathRewardComponent] {name}: 未配置 EnemyAttributesSO，无法提交死亡奖励。",
                    this);
                return;
            }

            if (enemy.Attributes.rewardMode == EnemyRewardMode.Ignore)
            {
                return;
            }

            if (!ValidateDependencies())
            {
                return;
            }

            _rewarded = true;
            if (!_combatPhase.CanPerformCombatActions)
            {
                return;
            }

            float gainedEnergy = _energy.AddEnergy(EnergyReward);
            int gainedPoints = _gameEventService.NotifyEnemyKilled(PointsReward);
            _positiveRewardGranted = gainedEnergy > 0f || gainedPoints > 0;
        }

        /// <summary>实体主动重置时允许下一次死亡再次提交奖励。</summary>
        private void HandleReuseRequested()
        {
            _rewarded = false;
            _positiveRewardGranted = false;
        }

        /// <summary>响应宿主广播，补做死亡事件订阅。</summary>
        void IEnemyActivationListener.OnEnemyActivationReady()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            _enemy ??= GetComponent<Enemy>();
            if (_enemy != null && !_subscribed)
            {
                _enemy.OnDied += HandleDeath;
                _enemy.ReuseRequested += HandleReuseRequested;
                _subscribed = true;
            }
        }

        /// <summary>检查奖励链所需的战斗期服务是否已由 VContainer 注入。</summary>
        /// <returns>依赖完整时返回 true。</returns>
        private bool ValidateDependencies()
        {
            if (_energy != null &&
                _gameEventService != null &&
                _combatPhase != null)
            {
                return true;
            }

            if (!_dependencyErrorReported)
            {
                Debug.LogError(
                    $"[EnemyDeathRewardComponent] {name}: " +
                    "需要 IEnergyService、IGameEventService 和 ICombatPhaseService。",
                    this);
                _dependencyErrorReported = true;
            }

            return false;
        }
    }
}
