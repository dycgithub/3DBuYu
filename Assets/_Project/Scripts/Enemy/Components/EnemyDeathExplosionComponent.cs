using CombatSystem;
using EffectSystem;
using Interfaces;
using Services;
using UnityEngine;
using VContainer;

namespace EnemySystem.Components
{
    /// <summary>
    /// 自爆敌人的死亡范围伤害行为。
    /// <para>爆炸参数由所属 <see cref="EnemyAttributesSO"/> 提供，伤害仍走统一目标入口。</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    public class EnemyDeathExplosionComponent : MonoBehaviour, IEnemyActivationListener
    {
        [Header("调试")]
        [SerializeField] private bool showGizmo = true;

        [Inject] private IEffectService _effectService;
        [Inject] private ISpatialQueryService _spatialService;
        [Inject] private IDamageRequestProcessor _damageProcessor;
        [Inject] private IPlayerCombatTarget _playerTarget;

        private Enemy _enemy;
        private bool _subscribed;

        /// <summary>缓存敌人引用。</summary>
        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
        }

        /// <summary>注册死亡事件。</summary>
        private void OnEnable()
        {
            _enemy ??= GetComponent<Enemy>();
            if (_enemy != null && !_subscribed)
            {
                _enemy.OnDied += HandleDeath;
                _subscribed = true;
            }
        }

        /// <summary>注销死亡事件，避免对象池实例重复订阅。</summary>
        private void OnDisable()
        {
            if (_enemy != null && _subscribed)
            {
                _enemy.OnDied -= HandleDeath;
                _subscribed = false;
            }
        }

        /// <summary>对范围内敌人和玩家造成一次爆炸伤害。</summary>
        /// <param name="enemy">触发死亡事件的敌人。</param>
        private void HandleDeath(Enemy enemy)
        {
            if (enemy == null || enemy.DeathCause == EnemyDeathCause.Consumed)
            {
                return;
            }

            EnemyAttributesSO attributes = enemy.Attributes;
            float radius = attributes != null ? Mathf.Max(0f, attributes.explosionRadius) : 0f;
            float damage = attributes != null
                ? Mathf.Max(0f, attributes.explosionDamage) * enemy.GetAttackDamageMultiplier()
                : 0f;
            if (radius > 0f && damage > 0f)
            {
                if (_spatialService != null)
                {
                    var targets = _spatialService.QueryRadius(
                        transform.position,
                        radius,
                        SpatialIndexing.SpatialLayers.Enemy);
                    for (int index = 0; index < targets.Count; index++)
                    {
                        if (targets[index] is not Enemy targetEnemy || targetEnemy != enemy)
                        {
                            ApplyDamage(targets[index], damage);
                        }
                    }
                }

                if (_playerTarget != null &&
                    _playerTarget.Transform != null &&
                    Vector3.Distance(_playerTarget.Transform.position, transform.position) <= radius)
                {
                    ApplyDamage(_playerTarget, damage);
                }
            }

            _effectService?.Play(EffectId.BigExplosion, transform.position);
        }

        /// <summary>提交一次范围伤害请求。</summary>
        private void ApplyDamage(IDamageable target, float damage)
        {
            if (_damageProcessor == null || !target.IsAliveAndValid())
            {
                return;
            }

            _damageProcessor.TryApplyDamage(
                target,
                new DamageRequest
                {
                    SourceInstanceId = _enemy != null ? _enemy.GetInstanceID() : 0,
                    DamageAmount = damage,
                    DamageType = DamageType.Physical,
                    HitPosition = target.Position,
                    HitNormal = Vector3.up
                },
                out _);
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
                _subscribed = true;
            }
        }

        /// <summary>在编辑器中显示来自敌人配置的爆炸范围。</summary>
        private void OnDrawGizmosSelected()
        {
            if (!showGizmo)
            {
                return;
            }

            Enemy enemy = GetComponent<Enemy>();
            float radius = enemy != null && enemy.Attributes != null
                ? Mathf.Max(0f, enemy.Attributes.explosionRadius)
                : 0f;
            if (radius <= 0f)
            {
                return;
            }

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, radius);
        }

    }
}
