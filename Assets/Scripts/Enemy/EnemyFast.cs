using CameraSystem;
using EffectSystem;
using PlayerSystem;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// 快速敌人
    /// 高移速、低血量、快速攻击
    /// </summary>
    public class EnemyFast : EnemyBase
    {
        [Header("快速型特性")]
        [Tooltip("冲刺速度倍数")]
        public float sprintMultiplier = 1.5f;

        [Tooltip("冲刺持续时间")]
        public float sprintDuration = 2f;

        [Tooltip("冲刺冷却时间")]
        public float sprintCooldown = 3f;

        [Tooltip("闪避概率")]
        [Range(0f, 1f)]
        public float dodgeChance = 0.3f;

        // 冲刺状态
        private float sprintTimer;
        private float sprintCooldownTimer;
        private bool isSprinting;

        protected override void Awake()
        {
            base.Awake();
            // 快速型基础属性设置
            maxHealth = 50f;
            moveSpeed = 6f;
            attackDamage = 5f;
            attackCooldown = 0.5f;
            enemyType = EnemyType.Fast;
        }

        protected override void Update()
        {
            base.Update();
            UpdateSprint();
        }

        /// <summary>
        /// 更新冲刺状态
        /// </summary>
        private void UpdateSprint()
        {
            if (isSprinting)
            {
                sprintTimer -= Time.deltaTime;
                if (sprintTimer <= 0)
                {
                    isSprinting = false;
                    sprintCooldownTimer = sprintCooldown;
                }
            }
            else
            {
                if (sprintCooldownTimer > 0)
                {
                    sprintCooldownTimer -= Time.deltaTime;
                }
                else if (currentState == EnemyState.Chase)
                {
                    // 追踪状态下开始冲刺
                    StartSprint();
                }
            }
        }

        /// <summary>
        /// 开始冲刺
        /// </summary>
        private void StartSprint()
        {
            isSprinting = true;
            sprintTimer = sprintDuration;
            PlayAnimation("Sprint");

            // 播放冲刺特效
            EffectManager.Instance?.PlayEffect("EnemySprint", transform.position);
        }

        protected override void OnChase()
        {
            if (target == null) return;

            // 冲刺时速度加快
            float currentSpeed = isSprinting ? moveSpeed * sprintMultiplier : moveSpeed;

            // 直接移动而不使用基类的MoveTowards
            Vector3 direction = (target.position - transform.position).normalized;
            direction.y = 0;

            if (direction != Vector3.zero)
            {
                Vector3 newPosition = transform.position + direction * currentSpeed * Time.deltaTime;
                transform.position = newPosition;
            }

            RotateTowards(target.position);
        }

        protected override void OnPatrol()
        {
            // 快速型巡逻时也会快速移动
            Vector3 randomDirection = Random.insideUnitSphere;
            randomDirection.y = 0;
            randomDirection.Normalize();

            Vector3 patrolTarget = transform.position + randomDirection * 3f;
            MoveTowards(patrolTarget);
            RotateTowards(patrolTarget);
        }

        protected override void PerformAttack()
        {
            if (target == null) return;

            // 快速连击
            var playerHealth = target.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(attackDamage);
            }

            // 快速攻击后闪避
            if (Random.value < dodgeChance)
            {
                PerformDodge();
            }
        }

        /// <summary>
        /// 执行闪避
        /// </summary>
        private void PerformDodge()
        {
            Vector3 dodgeDirection = Random.insideUnitSphere;
            dodgeDirection.y = 0;
            dodgeDirection.Normalize();

            Vector3 dodgeTarget = transform.position + dodgeDirection * 2f;
            transform.position = Vector3.Lerp(transform.position, dodgeTarget, 0.5f);

            PlayAnimation("Dodge");
            EffectManager.Instance?.PlayEffect("EnemyDodge", transform.position);
        }

        public override void TakeDamage(float damage)
        {
            // 受击时有几率闪避
            if (Random.value < dodgeChance)
            {
                PerformDodge();
                // 播放闪避文字提示
                Debug.Log("闪避!");
                return;
            }

            base.TakeDamage(damage);
        }
    }
}
