using CombatSystem;
using Interfaces;
using Services;
using SpatialSystem.Bridge;
using UnityEngine;
using Utils;
using VContainer;

namespace EnemySystem.Components
{
    /// <summary>
    /// 周期性范围减速或治疗效果。
    /// 同一组件通过枚举区分三种目标规则，避免复制空间查询流程。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    public sealed class EnemyAreaEffectComponent : MonoBehaviour, IPooledObject, IEnemyActivationListener
    {
        [Inject] private ISpatialQueryService _spatialService;
        [Inject] private IPlayerCombatTarget _playerTarget;

        private Enemy _enemy;
        private BuffDefinitionSO _runtimeSlowBuff;
        private float _tickTimer;

        /// <summary>缓存宿主。</summary>
        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
        }

        /// <summary>启用时立即允许第一次结算。</summary>
        private void OnEnable()
        {
            _tickTimer = 0f;
        }

        /// <summary>释放本组件创建的运行时减速定义。</summary>
        private void OnDestroy()
        {
            if (_runtimeSlowBuff != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_runtimeSlowBuff);
                }
                else
                {
                    DestroyImmediate(_runtimeSlowBuff);
                }

                _runtimeSlowBuff = null;
            }
        }

        /// <summary>按范围效果配置周期结算。</summary>
        private void Update()
        {
            if (_enemy == null || _enemy.IsDead || _enemy.Attributes == null)
            {
                return;
            }

            _tickTimer -= Time.deltaTime;
            if (_tickTimer > 0f)
            {
                return;
            }

            float interval = Mathf.Max(0.01f, _enemy.Attributes.areaEffectTickInterval);
            _tickTimer = interval;
            ApplyEffect(interval);
        }

        /// <summary>执行一次范围效果。</summary>
        private void ApplyEffect(float interval)
        {
            EnemyAttributesSO attributes = _enemy.Attributes;
            if (_spatialService != null)
            {
                var targets = _spatialService.QueryRadius(
                    transform.position,
                    Mathf.Max(0f, attributes.areaEffectRadius),
                    SpatialRegistry.LAYER_ENEMY);
                for (int index = 0; index < targets.Count; index++)
                {
                    ApplyToEnemy(targets[index], attributes, interval);
                }
            }

            if (attributes.areaEffectType == EnemyAreaEffectType.HealAll &&
                IsPlayerInRange(attributes.areaEffectRadius))
            {
                Heal(_playerTarget, attributes.areaHealPerSecond * interval);
            }
            else if (attributes.areaEffectType == EnemyAreaEffectType.Slow &&
                     IsPlayerInRange(attributes.areaEffectRadius))
            {
                _playerTarget?.ApplyBuff(GetSlowBuff(attributes), GetInstanceID());
            }
        }

        /// <summary>将效果应用到一个敌人目标。</summary>
        private void ApplyToEnemy(IDamageable target, EnemyAttributesSO attributes, float interval)
        {
            if (target is not Enemy enemy || enemy.IsDead)
            {
                return;
            }

            switch (attributes.areaEffectType)
            {
                case EnemyAreaEffectType.Slow:
                    enemy.ApplyBuff(GetSlowBuff(attributes), GetInstanceID());
                    break;
                case EnemyAreaEffectType.HealAll:
                case EnemyAreaEffectType.HealEnemies:
                    Heal(enemy, attributes.areaHealPerSecond * interval);
                    break;
            }
        }

        /// <summary>返回按当前敌人配置生成的运行时减速定义。</summary>
        private BuffDefinitionSO GetSlowBuff(EnemyAttributesSO attributes)
        {
            if (attributes == null || attributes.areaSlowBuff == null)
            {
                return null;
            }

            if (_runtimeSlowBuff == null)
            {
                _runtimeSlowBuff = ScriptableObject.CreateInstance<BuffDefinitionSO>();
                _runtimeSlowBuff.name = $"{name}_SlowBuff";
                _runtimeSlowBuff.ModifierType = attributes.areaSlowBuff.ModifierType;
                _runtimeSlowBuff.Polarity = attributes.areaSlowBuff.Polarity;
                _runtimeSlowBuff.DurationSeconds = attributes.areaSlowBuff.DurationSeconds;
                _runtimeSlowBuff.StackPolicy = attributes.areaSlowBuff.StackPolicy;
                _runtimeSlowBuff.MaximumStackCount = attributes.areaSlowBuff.MaximumStackCount;
                _runtimeSlowBuff.VisualEffectPrefab = attributes.areaSlowBuff.VisualEffectPrefab;
            }

            _runtimeSlowBuff.ModifierValue = Mathf.Clamp(
                attributes.areaSlowMultiplier,
                0f,
                1f);
            return _runtimeSlowBuff;
        }

        /// <summary>给可治疗目标恢复资源。</summary>
        private static void Heal(IHealable target, float amount)
        {
            if (target != null)
            {
                target.Heal(amount);
            }
        }

        /// <summary>判断玩家是否在范围内。</summary>
        private bool IsPlayerInRange(float radius)
        {
            return _playerTarget != null &&
                _playerTarget.IsAlive &&
                _playerTarget.Transform != null &&
                Vector3.Distance(transform.position, _playerTarget.Transform.position) <= radius;
        }

        /// <summary>对象池租借时清除计时状态。</summary>
        public void OnRentFromPool()
        {
            _tickTimer = 0f;
        }

        /// <summary>对象池归还时清除计时状态。</summary>
        public void OnReturnToPool()
        {
            _tickTimer = 0f;
        }

        /// <summary>补做宿主激活后的依赖缓存。</summary>
        void IEnemyActivationListener.OnEnemyActivationReady()
        {
            _enemy ??= GetComponent<Enemy>();
        }
    }
}
