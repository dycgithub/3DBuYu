using UnityEngine;
using System.Collections;
using EffectSystem;
using GameSystem;

namespace EnemySystem
{
    /// <summary>
    /// 敌人类型枚举
    /// </summary>
    public enum EnemyType
    {
        Normal,     // 普通型
        Fast,       // 快速型
        Tank,       // 坦克型
        Flying      // 飞行型
    }

    /// <summary>
    /// 敌人状态枚举
    /// </summary>
    public enum EnemyState
    {
        Idle,       // 待机
        Patrol,     // 巡逻
        Chase,      // 追踪
        Attack,     // 攻击
        Dead        // 死亡
    }

    /// <summary>
    /// 敌人基类
    /// 所有敌人类型的父类，提供通用功能和接口
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public abstract class EnemyBase : MonoBehaviour
    {
        [Header("基础属性")]
        [Tooltip("敌人类型")]
        public EnemyType enemyType = EnemyType.Normal;

        [Tooltip("最大生命值")]
        public float maxHealth = 100f;

        [Tooltip("移动速度")]
        public float moveSpeed = 3f;

        [Tooltip("旋转速度")]
        public float rotationSpeed = 5f;

        [Tooltip("攻击力")]
        public float attackDamage = 10f;

        [Tooltip("攻击范围")]
        public float attackRange = 1.5f;

        [Tooltip("攻击冷却时间")]
        public float attackCooldown = 1f;

        [Header("检测设置")]
        [Tooltip("玩家检测范围")]
        public float detectionRange = 10f;

        [Tooltip("丢失目标距离")]
        public float loseTargetDistance = 15f;

        [Header("掉落设置")]
        [Tooltip("金币掉落数量")]
        public int coinDropAmount = 10;

        [Tooltip("经验值")]
        public int experienceValue = 20;

        [Tooltip("掉落概率 (0-1)")]
        [Range(0f, 1f)]
        public float dropChance = 0.5f;

        [Header("视觉效果")]
        [Tooltip("死亡特效预制体")]
        public GameObject deathEffectPrefab;

        [Tooltip("受击特效预制体")]
        public GameObject hitEffectPrefab;

        [Header("调试")]
        [Tooltip("是否显示调试信息")]
        public bool showDebugInfo = true;

        // 当前状态
        protected EnemyState currentState = EnemyState.Idle;
        protected float currentHealth;
        protected Transform target;
        protected float lastAttackTime;
        protected bool isDead = false;

        // 组件缓存
        protected Collider enemyCollider;
        protected Rigidbody enemyRigidbody;
        protected Animator animator;

        #region 属性

        /// <summary>
        /// 当前生命值
        /// </summary>
        public float CurrentHealth => currentHealth;

        /// <summary>
        /// 当前状态
        /// </summary>
        public EnemyState CurrentState => currentState;

        /// <summary>
        /// 是否死亡
        /// </summary>
        public bool IsDead => isDead;

        /// <summary>
        /// 生命值百分比
        /// </summary>
        public float HealthPercent => currentHealth / maxHealth;

        #endregion

        #region Unity生命周期

        protected virtual void Awake()
        {
            enemyCollider = GetComponent<Collider>();
            enemyRigidbody = GetComponent<Rigidbody>();
            animator = GetComponentInChildren<Animator>();
        }

        protected virtual void Start()
        {
            Initialize();
        }

        protected virtual void Update()
        {
            if (isDead) return;

            UpdateState();
            ExecuteCurrentState();
        }

        protected virtual void OnDrawGizmosSelected()
        {
            if (!showDebugInfo) return;

            // 检测范围
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);

            // 攻击范围
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);

            // 丢失目标距离
            Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(transform.position, loseTargetDistance);

            // 目标连线
            if (target != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, target.position);
            }
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化敌人
        /// </summary>
        protected virtual void Initialize()
        {
            currentHealth = maxHealth;
            currentState = EnemyState.Idle;
            isDead = false;
            lastAttackTime = -attackCooldown;

            if (enemyCollider != null)
                enemyCollider.enabled = true;
        }

        /// <summary>
        /// 设置目标（通常由生成器调用）
        /// </summary>
        public virtual void SetTarget(Transform playerTarget)
        {
            target = playerTarget;
        }

        #endregion

        #region 状态机

        /// <summary>
        /// 更新状态
        /// </summary>
        protected virtual void UpdateState()
        {
            if (target == null)
            {
                FindTarget();
                if (target == null)
                {
                    ChangeState(EnemyState.Patrol);
                    return;
                }
            }

            float distanceToTarget = Vector3.Distance(transform.position, target.position);

            // 丢失目标
            if (distanceToTarget > loseTargetDistance)
            {
                target = null;
                ChangeState(EnemyState.Patrol);
                return;
            }

            // 在攻击范围内
            if (distanceToTarget <= attackRange)
            {
                ChangeState(EnemyState.Attack);
                return;
            }

            // 检测到目标
            if (distanceToTarget <= detectionRange)
            {
                ChangeState(EnemyState.Chase);
                return;
            }

            // 默认巡逻
            ChangeState(EnemyState.Patrol);
        }

        /// <summary>
        /// 切换状态
        /// </summary>
        protected virtual void ChangeState(EnemyState newState)
        {
            if (currentState == newState) return;

            OnExitState(currentState);
            currentState = newState;
            OnEnterState(currentState);
        }

        /// <summary>
        /// 进入状态时调用
        /// </summary>
        protected virtual void OnEnterState(EnemyState state)
        {
            switch (state)
            {
                case EnemyState.Idle:
                    PlayAnimation("Idle");
                    break;
                case EnemyState.Patrol:
                    PlayAnimation("Walk");
                    break;
                case EnemyState.Chase:
                    PlayAnimation("Run");
                    break;
                case EnemyState.Attack:
                    PlayAnimation("Attack");
                    break;
            }
        }

        /// <summary>
        /// 退出状态时调用
        /// </summary>
        protected virtual void OnExitState(EnemyState state)
        {
            // 子类可重写
        }

        /// <summary>
        /// 执行当前状态的行为
        /// </summary>
        protected virtual void ExecuteCurrentState()
        {
            switch (currentState)
            {
                case EnemyState.Idle:
                    OnIdle();
                    break;
                case EnemyState.Patrol:
                    OnPatrol();
                    break;
                case EnemyState.Chase:
                    OnChase();
                    break;
                case EnemyState.Attack:
                    OnAttack();
                    break;
            }
        }

        #endregion

        #region 状态行为（可重写）

        /// <summary>
        /// 待机行为
        /// </summary>
        protected virtual void OnIdle()
        {
            // 基础实现为空，子类可重写
        }

        /// <summary>
        /// 巡逻行为
        /// </summary>
        protected abstract void OnPatrol();

        /// <summary>
        /// 追踪行为
        /// </summary>
        protected virtual void OnChase()
        {
            if (target == null) return;

            MoveTowards(target.position);
            RotateTowards(target.position);
        }

        /// <summary>
        /// 攻击行为
        /// </summary>
        protected virtual void OnAttack()
        {
            if (target == null) return;

            // 面向目标
            RotateTowards(target.position);

            // 检查攻击冷却
            if (Time.time - lastAttackTime >= attackCooldown)
            {
                PerformAttack();
                lastAttackTime = Time.time;
            }
        }

        /// <summary>
        /// 执行攻击（子类实现具体攻击逻辑）
        /// </summary>
        protected abstract void PerformAttack();

        #endregion

        #region 移动与旋转

        /// <summary>
        /// 向目标位置移动
        /// </summary>
        protected virtual void MoveTowards(Vector3 targetPosition)
        {
            Vector3 direction = (targetPosition - transform.position).normalized;
            direction.y = 0; // 保持水平移动

            if (direction != Vector3.zero)
            {
                Vector3 newPosition = transform.position + direction * moveSpeed * Time.deltaTime;
                transform.position = newPosition;
            }
        }

        /// <summary>
        /// 旋转朝向目标
        /// </summary>
        protected virtual void RotateTowards(Vector3 targetPosition)
        {
            Vector3 direction = (targetPosition - transform.position).normalized;
            direction.y = 0;

            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    rotationSpeed * Time.deltaTime
                );
            }
        }

        #endregion

        #region 战斗

        /// <summary>
        /// 搜索目标
        /// </summary>
        protected virtual void FindTarget()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                float distance = Vector3.Distance(transform.position, player.transform.position);
                if (distance <= detectionRange)
                {
                    target = player.transform;
                }
            }
        }

        /// <summary>
        /// 受到伤害
        /// </summary>
        public virtual void TakeDamage(float damage)
        {
            if (isDead) return;

            currentHealth -= damage;

            // 播放受击特效
            PlayHitEffect();

            // 播放受击动画
            PlayAnimation("Hit");

            // 触发受伤事件
            OnDamageTaken(damage);

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        /// <summary>
        /// 受伤时调用（子类可重写）
        /// </summary>
        protected virtual void OnDamageTaken(float damage)
        {
            // 子类可重写
        }

        /// <summary>
        /// 死亡
        /// </summary>
        protected virtual void Die()
        {
            if (isDead) return;

            isDead = true;
            currentState = EnemyState.Dead;

            // 禁用碰撞器
            if (enemyCollider != null)
                enemyCollider.enabled = false;

            // 播放死亡特效
            PlayDeathEffect();

            // 播放死亡动画
            PlayAnimation("Death");

            // 掉落物品
            DropItems();

            // 触发死亡事件
            OnDeath();

            // 延迟销毁
            StartCoroutine(DestroyAfterDelay(3f));
        }

        /// <summary>
        /// 死亡时调用（子类可重写）
        /// </summary>
        protected virtual void OnDeath()
        {
            // 通知游戏管理器
            GameManager.Instance?.OnEnemyKilled(this);
        }

        /// <summary>
        /// 延迟销毁
        /// </summary>
        private IEnumerator DestroyAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            Destroy(gameObject);
        }

        #endregion

        #region 掉落系统

        /// <summary>
        /// 掉落物品
        /// </summary>
        protected virtual void DropItems()
        {
            // 通知掉落管理器
            DropManager.Instance?.SpawnDrops(
                transform.position,
                coinDropAmount,
                experienceValue,
                dropChance
            );
        }

        #endregion

        #region 特效与动画

        /// <summary>
        /// 播放动画
        /// </summary>
        protected virtual void PlayAnimation(string animationName)
        {
            if (animator != null)
            {
                animator.SetTrigger(animationName);
            }
        }

        /// <summary>
        /// 播放死亡特效
        /// </summary>
        protected virtual void PlayDeathEffect()
        {
            if (deathEffectPrefab != null)
            {
                Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
            }

            // 使用特效管理器
            EffectManager.Instance?.PlayEffect("EnemyDeath", transform.position);
        }

        /// <summary>
        /// 播放受击特效
        /// </summary>
        protected virtual void PlayHitEffect()
        {
            if (hitEffectPrefab != null)
            {
                Instantiate(hitEffectPrefab, transform.position, Quaternion.identity);
            }

            // 使用特效管理器
            EffectManager.Instance?.PlayEffect("EnemyHit", transform.position);
        }

        #endregion
    }
}
