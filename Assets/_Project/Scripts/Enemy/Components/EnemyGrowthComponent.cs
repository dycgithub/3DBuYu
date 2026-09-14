using Interfaces;
using Services;
using SpatialSystem.Bridge;
using UnityEngine;
using Utils;
using VContainer;

namespace EnemySystem.Components
{
    /// <summary>
    /// 成长敌人的周期吞噬行为。
    /// 使用空间查询寻找其他敌人，避免依赖敌人 Rigidbody。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    public sealed class EnemyGrowthComponent : MonoBehaviour, IPooledObject, IEnemyActivationListener
    {
        [Inject] private ISpatialQueryService _spatialService;

        private Enemy _enemy;
        private float _checkTimer;

        /// <summary>缓存宿主并清除计时器。</summary>
        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
        }

        /// <summary>对象池启用时清除运行时计时。</summary>
        private void OnEnable()
        {
            _checkTimer = 0f;
        }

        /// <summary>按配置间隔尝试吞噬附近敌人。</summary>
        private void Update()
        {
            if (_enemy == null || _enemy.IsDead || _enemy.Attributes == null)
            {
                return;
            }

            _checkTimer -= Time.deltaTime;
            if (_checkTimer > 0f)
            {
                return;
            }

            _checkTimer = Mathf.Max(0.01f, _enemy.Attributes.growthCheckInterval);
            if (Random.value > Mathf.Clamp01(_enemy.Attributes.growthConsumeChance))
            {
                return;
            }

            TryConsumeNearbyEnemy();
        }

        /// <summary>公开一次吞噬尝试，供行为测试和其他机制复用。</summary>
        public bool TryConsume(Enemy target)
        {
            return _enemy != null && _enemy.TryAbsorb(target);
        }

        /// <summary>查询并吞噬距离最近的其他敌人。</summary>
        private void TryConsumeNearbyEnemy()
        {
            if (_spatialService == null)
            {
                return;
            }

            var targets = _spatialService.QueryRadius(
                transform.position,
                Mathf.Max(0f, _enemy.Attributes.growthConsumeRadius),
                SpatialRegistry.LAYER_ENEMY);
            for (int index = 0; index < targets.Count; index++)
            {
                if (targets[index] is Enemy target && target != _enemy && TryConsume(target))
                {
                    return;
                }
            }
        }

        /// <summary>对象池租借时清除计时状态。</summary>
        public void OnRentFromPool()
        {
            _checkTimer = 0f;
        }

        /// <summary>对象池归还时清除计时状态。</summary>
        public void OnReturnToPool()
        {
            _checkTimer = 0f;
        }

        /// <summary>补做宿主激活后的引用缓存。</summary>
        void IEnemyActivationListener.OnEnemyActivationReady()
        {
            _enemy ??= GetComponent<Enemy>();
        }
    }
}
