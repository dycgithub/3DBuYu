using System.Collections.Generic;
using CombatSystem;
using Interfaces;
using Services;
using UnityEngine;
using Utils;
using VContainer;

namespace EnemySystem.Components
{
    /// <summary>
    /// 敌人与玩家发生 Trigger 接触时造成能量伤害，并通知可选的接触死亡行为。
    /// <para>冷却状态随启用、禁用和对象池生命周期清理。</para>
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    [RequireComponent(typeof(Collider))]
    public sealed class EnemyContactDamageComponent : MonoBehaviour, IPooledObject
    {
        [Header("接触伤害")]
        [Tooltip("每次有效玩家接触造成的基础能量伤害。")]
        [SerializeField] private float damagePerContact = 1f;
        [Tooltip("同一玩家再次受到接触伤害前需要经过的秒数。")]
        [SerializeField] private float contactInterval = 1f;

        [Inject] private IEnergyService _energyService;
        [Inject] private IPlayerCombatTarget _playerTarget;
        [Inject] private IDamageRequestProcessor _damageProcessor;
        [Inject] private ICombatPhaseService _combatPhase;

        private readonly Dictionary<int, float> _lastContactTimes = new();
        private Enemy _enemy;
        private EnemyContactDeathComponent _contactDeath;

        /// <summary>缓存所属敌人。</summary>
        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
            _contactDeath = GetComponent<EnemyContactDeathComponent>();
        }

        /// <summary>启用时清理上一次租借留下的接触冷却。</summary>
        private void OnEnable()
        {
            _enemy ??= GetComponent<Enemy>();
            _contactDeath ??= GetComponent<EnemyContactDeathComponent>();
            ClearContactState();
        }

        /// <summary>禁用时清理接触冷却，防止停用期间的状态进入下一次启用。</summary>
        private void OnDisable()
        {
            ClearContactState();
        }

        /// <summary>处理敌人 Collider 发出的玩家 Trigger 接触。</summary>
        /// <param name="other">与敌人发生接触的另一侧 Collider。</param>
        private void OnTriggerEnter(Collider other)
        {
            TryDrainPlayerEnergy(other);
        }

        /// <summary>对象池租借时清理接触冷却。</summary>
        public void OnRentFromPool()
        {
            ClearContactState();
        }

        /// <summary>对象池归还时清理接触冷却。</summary>
        public void OnReturnToPool()
        {
            ClearContactState();
        }

        /// <summary>判断接触条件并提交一次玩家能量消耗。</summary>
        /// <param name="other">接触到的 Collider。</param>
        private void TryDrainPlayerEnergy(Collider other)
        {
            _enemy ??= GetComponent<Enemy>();
            if (!isActiveAndEnabled || other == null || _enemy == null || _enemy.IsDead)
            {
                return;
            }

            if ((_energyService == null && _playerTarget == null) ||
                _combatPhase == null ||
                !_combatPhase.CanPerformCombatActions)
            {
                return;
            }

            if (!IsPlayerCollider(other))
            {
                return;
            }

            int playerId = GetPlayerId(other);
            if (_lastContactTimes.TryGetValue(playerId, out float lastContactTime) &&
                Time.time - lastContactTime < Mathf.Max(0f, contactInterval))
            {
                return;
            }

            _lastContactTimes[playerId] = Time.time;
            float damage = Mathf.Max(0f, damagePerContact) * _enemy.GetAttackDamageMultiplier();
            if (_enemy.GetAttackAllowedMultiplier() > 0f && damage > 0f)
            {
                if (_playerTarget != null)
                {
                    if (_damageProcessor != null)
                    {
                        _damageProcessor.TryApplyDamage(
                            _playerTarget,
                            new DamageRequest
                            {
                                SourceInstanceId = _enemy.GetInstanceID(),
                                DamageAmount = damage,
                                DamageType = DamageType.Physical,
                                HitPosition = other.ClosestPoint(transform.position),
                                HitNormal = Vector3.up
                            },
                            out _);
                    }
                }
                else
                {
                    _energyService.Drain(damage, EnergySpendKind.EnemyContact);
                }
            }

            _contactDeath?.TryKillOnPlayerContact();
        }

        /// <summary>只接受现有 Player 标签，避免敌人之间互相触发。</summary>
        /// <param name="collider">待识别的接触 Collider。</param>
        /// <returns>Collider 或其根对象属于 Player 标签时返回 true。</returns>
        private static bool IsPlayerCollider(Collider collider)
        {
            return collider.CompareTag("Player") || collider.transform.root.CompareTag("Player");
        }

        /// <summary>优先按玩家 Rigidbody 归并多个碰撞体，否则按玩家根对象归并。</summary>
        /// <param name="collider">玩家接触 Collider。</param>
        /// <returns>玩家本次租借实例的稳定运行时标识。</returns>
        private static int GetPlayerId(Collider collider)
        {
            Rigidbody rigidbody = collider.attachedRigidbody;
            return rigidbody != null
                ? rigidbody.GetInstanceID()
                : collider.transform.root.gameObject.GetInstanceID();
        }

        /// <summary>清除当前敌人所有玩家接触的冷却时间。</summary>
        private void ClearContactState()
        {
            _lastContactTimes.Clear();
        }
    }
}
