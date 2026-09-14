using CombatSystem;
using Interfaces;
using Play;
using Services;
using UnityEngine;
using Utils;
using VContainer;

namespace EnemySystem.Components
{
    /// <summary>
    /// 远程射击敌人的攻击行为。
    /// 只负责目标筛选和冷却，投射物仍由通用发射器创建。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    [RequireComponent(typeof(EnemyCombatAbility))]
    public sealed class EnemyShooterComponent : MonoBehaviour, IPooledObject, IEnemyActivationListener
    {
        [Inject] private IPlayerCombatTarget _playerTarget;
        [Inject] private ICombatPhaseService _combatPhase;

        private Enemy _enemy;
        private EnemyCombatAbility _ability;
        private float _cooldown;

        /// <summary>缓存组件引用。</summary>
        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
            _ability = GetComponent<EnemyCombatAbility>();
        }

        /// <summary>启用时清除攻击冷却。</summary>
        private void OnEnable()
        {
            _cooldown = 0f;
        }

        /// <summary>按攻击间隔尝试向玩家发射投射物。</summary>
        private void Update()
        {
            if (_enemy == null || _enemy.IsDead || _enemy.Attributes == null)
            {
                return;
            }

            _playerTarget ??= FindFirstObjectByType<PlayerCombatTarget>();
            if (_playerTarget == null || !_playerTarget.IsAlive || _playerTarget.Transform == null)
            {
                return;
            }

            _cooldown -= Time.deltaTime;
            if (_cooldown > 0f ||
                (_combatPhase != null && !_combatPhase.CanPerformCombatActions))
            {
                return;
            }

            EnemyAttributesSO attributes = _enemy.Attributes;
            Vector3 offset = _playerTarget.Transform.position - transform.position;
            if (offset.sqrMagnitude > attributes.shooterAttackRadius * attributes.shooterAttackRadius)
            {
                return;
            }

            _cooldown = Mathf.Max(0.01f, attributes.shooterFireInterval);
            _ability?.LaunchProjectile(
                attributes.shooterProjectileProfile,
                attributes.shooterDamage,
                offset.normalized,
                1,
                0,
                ProjectileTargetMode.Player);
        }

        /// <summary>对象池租借时清除冷却。</summary>
        public void OnRentFromPool()
        {
            _cooldown = 0f;
        }

        /// <summary>对象池归还时清除冷却。</summary>
        public void OnReturnToPool()
        {
            _cooldown = 0f;
        }

        /// <summary>补做宿主激活后的组件缓存。</summary>
        void IEnemyActivationListener.OnEnemyActivationReady()
        {
            _enemy ??= GetComponent<Enemy>();
            _ability ??= GetComponent<EnemyCombatAbility>();
        }
    }
}
