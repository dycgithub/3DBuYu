using CombatSystem;
using System;
using Interfaces;
using Services;
using UnityEngine;
using Utils;
using VContainer;

namespace EnemySystem.Components
{
    /// <summary>
    /// 反弹敌人的短时间命中聚合器。
    /// 只有窗口内唯一一枚有效子弹命中时才创建反射投射物。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    public sealed class EnemyReflectComponent : MonoBehaviour, IPooledObject, IEnemyActivationListener
    {
        [Inject] private IProjectileHitEventSource _hitSource;
        [Inject] private IProjectileLauncher _projectileLauncher;

        private Enemy _enemy;
        private bool _subscribed;
        private bool _hasPendingHit;
        private int _hitCount;
        private float _remainingWindow;
        private ProjectileHitContext _firstHit;

        /// <summary>缓存宿主。</summary>
        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
        }

        /// <summary>启用时订阅命中事件。</summary>
        private void OnEnable()
        {
            _enemy ??= GetComponent<Enemy>();
            Subscribe();
            ResetPendingHits();
        }

        /// <summary>禁用时解除命中事件订阅。</summary>
        private void OnDisable()
        {
            Unsubscribe();
            ResetPendingHits();
        }

        /// <summary>等待命中窗口结束并决定是否反射。</summary>
        private void Update()
        {
            if (!_hasPendingHit)
            {
                return;
            }

            _remainingWindow -= Time.deltaTime;
            if (_remainingWindow > 0f)
            {
                return;
            }

            ProjectileHitContext hit = _firstHit;
            int hitCount = _hitCount;
            ResetPendingHits();
            if (hitCount == 1 && _enemy != null && !_enemy.IsDead)
            {
                Reflect(hit);
            }
        }

        /// <summary>收集命中敌人的投射物。</summary>
        private void HandleProjectileHit(ProjectileHitContext context)
        {
            if (_enemy == null ||
                context.Target is not Enemy target ||
                target != _enemy ||
                context.DamageResult.WasKilled)
            {
                return;
            }

            if (!_hasPendingHit)
            {
                _firstHit = context;
                _remainingWindow = Mathf.Max(
                    0f,
                    _enemy.Attributes != null ? _enemy.Attributes.reflectWindowSeconds : 0.05f);
                _hitCount = 0;
                _hasPendingHit = true;
            }

            _hitCount++;
        }

        /// <summary>创建一枚朝入射反方向飞向玩家的投射物。</summary>
        private void Reflect(ProjectileHitContext context)
        {
            if (_projectileLauncher == null || context.ProjectileData.ProjectileProfile == null)
            {
                return;
            }

            Vector3 direction = -context.ProjectileData.LaunchDirection;
            _projectileLauncher.Launch(new ProjectileLaunchRequest(
                _enemy.GetInstanceID(),
                context.ProjectileData.ProjectileProfile,
                transform.position,
                direction,
                context.ProjectileData.DamageAmount,
                1,
                0,
                context.ProjectileData.WasCritical,
                ProjectileTargetMode.Player));
        }

        /// <summary>订阅一次命中事件。</summary>
        private void Subscribe()
        {
            if (_hitSource != null && !_subscribed)
            {
                _hitSource.HitProcessed += HandleProjectileHit;
                _subscribed = true;
            }
        }

        /// <summary>注销命中事件。</summary>
        private void Unsubscribe()
        {
            if (_hitSource != null && _subscribed)
            {
                _hitSource.HitProcessed -= HandleProjectileHit;
                _subscribed = false;
            }
        }

        /// <summary>清除窗口内的待处理命中。</summary>
        private void ResetPendingHits()
        {
            _hasPendingHit = false;
            _hitCount = 0;
            _remainingWindow = 0f;
            _firstHit = default;
        }

        /// <summary>对象池租借时重新订阅并清理状态。</summary>
        public void OnRentFromPool()
        {
            Subscribe();
            ResetPendingHits();
        }

        /// <summary>对象池归还时幂等退订命中事件并清理待处理命中状态。</summary>
        public void OnReturnToPool()
        {
            Unsubscribe();
            ResetPendingHits();
        }

        /// <summary>补做宿主激活后的事件订阅。</summary>
        void IEnemyActivationListener.OnEnemyActivationReady()
        {
            _enemy ??= GetComponent<Enemy>();
            Subscribe();
        }
    }
}
