using EnemySystem;
using Services;
using UnityEngine;
using VContainer;

namespace EnemySystem.Components
{
    /// <summary>
    /// 敌人死亡奖励组件。
    /// 奖励值属于敌人 prefab 的配置；能量直接回到本局能量池，积分交给 GameManager 计入本局账并应用超时倍率。
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    public sealed class EnemyDeathRewardComponent : MonoBehaviour
    {
        [Header("死亡奖励")]
        [Tooltip("敌人死亡时回复的能量，不受超时倍率影响。")]
        [SerializeField, Min(0f)] private float energyReward;

        [Tooltip("敌人死亡时提供的基础 points，由 GameManager 按超时倍率计入本局。")]
        [SerializeField, Min(0)] private int pointsReward;

        [Inject] private ICombatEnergyService _energy;
        [Inject] private IGameEventService _gameEventService;
        [Inject] private ICombatPhaseService _combatPhase;

        private Enemy _enemy;
        private bool _subscribed;
        private bool _rewarded;

        public float EnergyReward => Mathf.Max(0f, energyReward);
        public int PointsReward => Mathf.Max(0, pointsReward);

        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
        }

        private void OnEnable()
        {
            _rewarded = false;
            _enemy ??= GetComponent<Enemy>();

            if (_enemy != null && !_subscribed)
            {
                _enemy.OnDied += HandleDeath;
                _subscribed = true;
            }
        }

        private void OnDisable()
        {
            if (_enemy != null && _subscribed)
            {
                _enemy.OnDied -= HandleDeath;
                _subscribed = false;
            }
        }

        private void HandleDeath(Enemy enemy)
        {
            if (_rewarded)
                return;

            _rewarded = true;
            if (_combatPhase != null && !_combatPhase.CanPerformCombatActions)
                return;

            _energy?.AddEnergy(EnergyReward);
            _gameEventService?.NotifyEnemyKilled(PointsReward);
        }
    }
}
