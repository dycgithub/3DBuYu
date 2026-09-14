using System.Collections.Generic;
using CombatSystem;
using UnityEngine;
using Utils;

namespace EnemySystem.Components
{
    /// <summary>
    /// 敌人的基础生命和受击处理组件。
    /// <para>组件保存生命运行时状态，Enemy 只负责对外暴露实体契约和生命周期事件。</para>
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    public sealed class EnemyHealthComponent : MonoBehaviour, IPooledObject
    {
        private readonly List<Enemy.EnemyDamageInterceptor> _interceptors = new(2);

        private Enemy _enemy;
        private float _currentHealth;
        private float _maxHealth;

        /// <summary>当前生命值。</summary>
        internal float CurrentHealth => _currentHealth;

        /// <summary>本次生成的最大生命值。</summary>
        internal float MaxHealth => _maxHealth;

        /// <summary>缓存所属敌人。</summary>
        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
        }

        /// <summary>启用时仅补齐宿主引用，生命状态由对象池回调或生成初始化负责。</summary>
        private void OnEnable()
        {
            if (_enemy == null)
            {
                _enemy = GetComponent<Enemy>();
            }
        }

        /// <summary>禁用时清除伤害拦截器，避免对象池复用残留。</summary>
        private void OnDisable()
        {
            _interceptors.Clear();
            ResetState();
        }

        /// <summary>由对象池租借回调清空旧生命状态。</summary>
        public void OnRentFromPool()
        {
            ResetState();
            _interceptors.Clear();
        }

        /// <summary>由对象池归还回调清空生命状态和伤害拦截器。</summary>
        public void OnReturnToPool()
        {
            ResetState();
            _interceptors.Clear();
        }

        /// <summary>
        /// 按本次生成倍率初始化生命值。
        /// </summary>
        /// <param name="healthMultiplier">本次生成使用的生命倍率。</param>
        internal void InitializeForSpawn(float healthMultiplier)
        {
            if (_enemy == null)
            {
                _enemy = GetComponent<Enemy>();
            }

            EnemyAttributesSO attributes = _enemy != null ? _enemy.Attributes : null;
            if (attributes == null)
            {
                ResetState();
                return;
            }

            _maxHealth = attributes.baseHealth * Mathf.Max(0.01f, healthMultiplier);
            _currentHealth = _maxHealth;
        }

        /// <summary>
        /// 处理一份伤害请求并在致死时通知实体宿主进入死亡流程；前置拦截器先于生命配置校验执行，完全闪避仍返回 Dodged。
        /// </summary>
        /// <param name="request">待处理的伤害请求。</param>
        /// <returns>伤害处理结果。</returns>
        internal DamageResult ReceiveDamageRequest(in DamageRequest request)
        {
            if (_enemy == null)
            {
                _enemy = GetComponent<Enemy>();
            }

            if (_enemy == null || _enemy.IsDead)
            {
                return InvalidResult();
            }

            float finalDamage = Mathf.Max(0f, request.DamageAmount);
            if (finalDamage <= 0f)
            {
                return BlockedResult();
            }

            if (request.DamageType != DamageType.Pure)
            {
                for (int index = 0; index < _interceptors.Count; index++)
                {
                    Enemy.EnemyDamageInterceptor interceptor = _interceptors[index];
                    if (!interceptor(_enemy, request.DamageAmount, ref finalDamage))
                    {
                        return new DamageResult
                        {
                            Outcome = DamageOutcome.Dodged,
                            RemainingHealth = _currentHealth
                        };
                    }
                }
            }

            if (_maxHealth <= 0f || _enemy.Attributes == null)
            {
                return InvalidResult();
            }

            float damageTakenMultiplier = _enemy.GetDamageTakenMultiplier(request.DamageType);
            float effectiveDamage = Mathf.Max(0f, finalDamage * damageTakenMultiplier);
            if (effectiveDamage <= 0f)
            {
                return BlockedResult();
            }

            _currentHealth -= effectiveDamage;
            _enemy.NotifyDamageTaken(effectiveDamage);

            bool wasKilled = _currentHealth <= 0f;
            if (wasKilled)
            {
                _enemy.DieFromHealth(EnemyDeathCause.Damage);
            }

            DamageResult result = new()
            {
                Outcome = wasKilled ? DamageOutcome.Killed : DamageOutcome.Applied,
                AppliedDamage = effectiveDamage,
                RemainingHealth = Mathf.Max(0f, _currentHealth),
                WasKilled = wasKilled
            };
            return result;
        }

        /// <summary>注册一个不重复的前置伤害拦截器。</summary>
        /// <param name="interceptor">要注册的拦截器。</param>
        internal void RegisterInterceptor(Enemy.EnemyDamageInterceptor interceptor)
        {
            if (interceptor != null && !_interceptors.Contains(interceptor))
            {
                _interceptors.Add(interceptor);
            }
        }

        /// <summary>移除一个前置伤害拦截器。</summary>
        /// <param name="interceptor">要移除的拦截器。</param>
        internal void UnregisterInterceptor(Enemy.EnemyDamageInterceptor interceptor)
        {
            if (interceptor != null)
            {
                _interceptors.Remove(interceptor);
            }
        }

        /// <summary>恢复生命值并返回实际恢复量。</summary>
        internal float Heal(float amount)
        {
            if (_enemy == null || _enemy.IsDead || _maxHealth <= 0f)
            {
                return 0f;
            }

            float previousHealth = _currentHealth;
            _currentHealth = Mathf.Min(
                _maxHealth,
                _currentHealth + Mathf.Max(0f, amount));
            return _currentHealth - previousHealth;
        }

        /// <summary>吸收一份生命值，同时扩张最大生命值。</summary>
        internal float AbsorbHealth(float amount)
        {
            if (_enemy == null || _enemy.IsDead || amount <= 0f)
            {
                return 0f;
            }

            float absorbed = Mathf.Max(0f, amount);
            _maxHealth += absorbed;
            _currentHealth += absorbed;
            return absorbed;
        }

        /// <summary>强制将当前生命归零，供强制死亡和吞噬使用。</summary>
        internal void SetHealthToZero()
        {
            _currentHealth = 0f;
        }

        /// <summary>重置生命值和本组件持有的防御拦截器。</summary>
        internal void ResetForReuse()
        {
            ResetState();
            _interceptors.Clear();
        }

        /// <summary>将组件恢复到尚未生成的状态。</summary>
        private void ResetState()
        {
            _currentHealth = 0f;
            _maxHealth = 0f;
        }

        /// <summary>创建目标已失效的结果。</summary>
        private DamageResult InvalidResult()
        {
            return new DamageResult
            {
                Outcome = DamageOutcome.Invalid,
                RemainingHealth = _currentHealth
            };
        }

        /// <summary>创建未造成有效伤害的结果。</summary>
        private DamageResult BlockedResult()
        {
            return new DamageResult
            {
                Outcome = DamageOutcome.Blocked,
                RemainingHealth = _currentHealth
            };
        }
    }
}
