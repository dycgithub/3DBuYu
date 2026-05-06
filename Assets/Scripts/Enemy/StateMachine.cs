using System;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// 状态机接口
    /// </summary>
    public interface IState
    {
        void OnEnter();
        void OnUpdate();
        void OnExit();
    }

    /// <summary>
    /// 泛型状态机
    /// 用于管理敌人的各种状态
    /// </summary>
    public class StateMachine<T> where T : Enum
    {
        private IState currentState;
        private T currentStateType;

        public IState CurrentState => currentState;
        public T CurrentStateType => currentStateType;

        /// <summary>
        /// 状态改变事件
        /// </summary>
        public event Action<T, T> OnStateChanged;

        /// <summary>
        /// 切换到新状态
        /// </summary>
        public void ChangeState(IState newState, T newStateType)
        {
            if (currentState != null)
            {
                currentState.OnExit();
            }

            T previousState = currentStateType;
            currentState = newState;
            currentStateType = newStateType;

            currentState?.OnEnter();
            OnStateChanged?.Invoke(previousState, newStateType);
        }

        /// <summary>
        /// 更新当前状态
        /// </summary>
        public void Update()
        {
            currentState?.OnUpdate();
        }
    }

    /// <summary>
    /// 敌人专用状态机
    /// </summary>
    public class EnemyStateMachine : StateMachine<EnemyState>
    {
        // 可以在这里添加敌人特有的状态机功能
    }

    /// <summary>
    /// 具体状态实现 - 待机状态
    /// </summary>
    public class IdleState : IState
    {
        private EnemyBase enemy;
        private float idleTimer;
        private float maxIdleTime;

        public IdleState(EnemyBase enemy, float maxIdleTime = 2f)
        {
            this.enemy = enemy;
            this.maxIdleTime = maxIdleTime;
        }

        public void OnEnter()
        {
            idleTimer = 0f;
            // 播放待机动画
            // enemy.PlayAnimation("Idle");
        }

        public void OnUpdate()
        {
            idleTimer += Time.deltaTime;

            // 待机时间结束或发现玩家，切换到其他状态
            if (idleTimer >= maxIdleTime)
            {
                // 这里应该由状态机管理器处理状态切换
            }
        }

        public void OnExit()
        {
            // 清理逻辑
        }
    }

    /// <summary>
    /// 具体状态实现 - 巡逻状态
    /// </summary>
    public class PatrolState : IState
    {
        private EnemyBase enemy;
        private Vector3 patrolTarget;
        private float patrolRadius;

        public PatrolState(EnemyBase enemy, float patrolRadius = 5f)
        {
            this.enemy = enemy;
            this.patrolRadius = patrolRadius;
        }

        public void OnEnter()
        {
            SetNewPatrolTarget();
        }

        public void OnUpdate()
        {
            // 巡逻逻辑在EnemyBase的OnPatrol中实现
        }

        public void OnExit()
        {
            // 清理逻辑
        }

        private void SetNewPatrolTarget()
        {
            // 设置新的巡逻目标点
        }
    }

    /// <summary>
    /// 具体状态实现 - 追踪状态
    /// </summary>
    public class ChaseState : IState
    {
        private EnemyBase enemy;
        private Transform target;

        public ChaseState(EnemyBase enemy, Transform target)
        {
            this.enemy = enemy;
            this.target = target;
        }

        public void OnEnter()
        {
            // 播放追踪动画
        }

        public void OnUpdate()
        {
            // 追踪逻辑在EnemyBase的OnChase中实现
        }

        public void OnExit()
        {
            // 清理逻辑
        }
    }

    /// <summary>
    /// 具体状态实现 - 攻击状态
    /// </summary>
    public class AttackState : IState
    {
        private EnemyBase enemy;
        private Transform target;
        private float attackCooldown;
        private float lastAttackTime;

        public AttackState(EnemyBase enemy, Transform target, float attackCooldown)
        {
            this.enemy = enemy;
            this.target = target;
            this.attackCooldown = attackCooldown;
            this.lastAttackTime = -attackCooldown;
        }

        public void OnEnter()
        {
            // 播放攻击准备动画
        }

        public void OnUpdate()
        {
            if (Time.time - lastAttackTime >= attackCooldown)
            {
                // 执行攻击
                lastAttackTime = Time.time;
            }
        }

        public void OnExit()
        {
            // 清理逻辑
        }
    }
}
