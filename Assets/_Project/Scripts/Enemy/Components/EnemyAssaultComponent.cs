using Interfaces;
using Services;
using UnityEngine;
using Utils;
using VContainer;
using FlockingSystem.ECS;

namespace EnemySystem.Components
{
    /// <summary>
    /// 进攻敌人的转向、蓄力和冲刺状态机。
    /// 冲刺期间暂停 ECS 位移，结束后恢复群游。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy))]
    [RequireComponent(typeof(EnemyFlockBridge))]
    public sealed class EnemyAssaultComponent : MonoBehaviour, IPooledObject, IEnemyActivationListener
    {
        [Inject] private IPlayerCombatTarget _playerTarget;

        private Enemy _enemy;
        private EnemyFlockBridge _flockBridge;
        private EnemyAssaultState _state;
        private float _stateTimer;
        private float _dashDistance;
        private Vector3 _dashDirection;

        /// <summary>当前状态。</summary>
        public EnemyAssaultState CurrentState => _state;

        /// <summary>缓存敌人和 Flocking 桥。</summary>
        private void Awake()
        {
            _enemy = GetComponent<Enemy>();
            _flockBridge = GetComponent<EnemyFlockBridge>();
            ResetState();
        }

        /// <summary>启用时清除上次对象池租借的状态。</summary>
        private void OnEnable()
        {
            ResetState();
        }

        /// <summary>禁用时恢复 ECS 对 Transform 的控制。</summary>
        private void OnDisable()
        {
            _flockBridge?.SetExternalMotion(false);
        }

        /// <summary>驱动进攻状态机。</summary>
        private void Update()
        {
            if (_enemy == null || _enemy.IsDead || _enemy.Attributes == null)
            {
                return;
            }

            if (_playerTarget == null || !_playerTarget.IsAlive || _playerTarget.Transform == null)
            {
                ReturnToRoaming();
                return;
            }

            switch (_state)
            {
                case EnemyAssaultState.Roaming:
                    TryStartTurning();
                    break;
                case EnemyAssaultState.Turning:
                    UpdateTurning();
                    break;
                case EnemyAssaultState.Charging:
                    UpdateCharging();
                    break;
                case EnemyAssaultState.Dashing:
                    UpdateDashing();
                    break;
            }
        }

        /// <summary>尝试从漫游切换到转向。</summary>
        private void TryStartTurning()
        {
            if (Vector3.Distance(transform.position, _playerTarget.Transform.position) <=
                _enemy.Attributes.assaultPerceptionRadius)
            {
                _state = EnemyAssaultState.Turning;
                _stateTimer = 0f;
                _flockBridge?.SetExternalMotion(true);
            }
        }

        /// <summary>逐帧将敌人朝玩家方向旋转。</summary>
        private void UpdateTurning()
        {
            Vector3 direction = GetTargetDirection();
            if (direction.sqrMagnitude <= 0.0001f)
            {
                ReturnToRoaming();
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                _enemy.Attributes.assaultTurnSpeed * Time.deltaTime);

            if (Quaternion.Angle(transform.rotation, targetRotation) <=
                _enemy.Attributes.assaultTurnTolerance)
            {
                _state = EnemyAssaultState.Charging;
                _stateTimer = 0f;
            }
        }

        /// <summary>完成蓄力后锁定冲刺方向。</summary>
        private void UpdateCharging()
        {
            _stateTimer += Time.deltaTime;
            if (_stateTimer < _enemy.Attributes.assaultChargeDuration)
            {
                return;
            }

            _dashDirection = GetTargetDirection();
            if (_dashDirection.sqrMagnitude <= 0.0001f)
            {
                ReturnToRoaming();
                return;
            }

            _dashDirection.Normalize();
            _dashDistance = 0f;
            _state = EnemyAssaultState.Dashing;
        }

        /// <summary>沿锁定方向推进冲刺，并在到达距离后恢复群游。</summary>
        private void UpdateDashing()
        {
            float speed = Mathf.Max(0f, _enemy.Attributes.assaultDashSpeed * _enemy.GetMovementSpeedMultiplier());
            float remainingDistance = Mathf.Max(0f, _enemy.Attributes.assaultDashDistance - _dashDistance);
            float distance = Mathf.Min(speed * Time.deltaTime, remainingDistance);
            transform.position += _dashDirection * distance;
            _dashDistance += distance;

            if (_dashDistance >= _enemy.Attributes.assaultDashDistance)
            {
                ReturnToRoaming();
            }
        }

        /// <summary>返回群游漫游状态。</summary>
        private void ReturnToRoaming()
        {
            _state = EnemyAssaultState.Roaming;
            _stateTimer = 0f;
            _dashDistance = 0f;
            _flockBridge?.SetExternalMotion(false);
        }

        /// <summary>清除状态机运行时数据。</summary>
        private void ResetState()
        {
            _state = EnemyAssaultState.Roaming;
            _stateTimer = 0f;
            _dashDistance = 0f;
            _dashDirection = Vector3.zero;
        }

        /// <summary>获取当前敌人到玩家的方向。</summary>
        private Vector3 GetTargetDirection()
        {
            return _playerTarget.Transform.position - transform.position;
        }

        /// <summary>对象池租借时恢复状态。</summary>
        public void OnRentFromPool()
        {
            ResetState();
        }

        /// <summary>对象池归还时恢复状态并释放外部运动。</summary>
        public void OnReturnToPool()
        {
            ResetState();
            _flockBridge?.SetExternalMotion(false);
        }

        /// <summary>补做注入后的运行时初始化。</summary>
        void IEnemyActivationListener.OnEnemyActivationReady()
        {
            _enemy ??= GetComponent<Enemy>();
            _flockBridge ??= GetComponent<EnemyFlockBridge>();
        }
    }
}
