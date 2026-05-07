
using EffectSystem;
using PlayerSystem;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// 飞行敌人
    /// 无视地形、高空优势、俯冲攻击
    /// </summary>
    public class EnemyFlying : EnemyBase
    {
        [Header("飞行型特性")]
        [Tooltip("飞行高度")]
        public float flyHeight = 5f;

        [Tooltip("俯冲攻击距离")]
        public float diveAttackRange = 8f;

        [Tooltip("俯冲攻击高度")]
        public float diveHeight = 2f;

        [Tooltip("俯冲攻击伤害")]
        public float diveAttackDamage = 15f;

        [Tooltip("飞行盘旋半径")]
        public float circleRadius = 3f;

        [Tooltip("飞行盘旋速度")]
        public float circleSpeed = 2f;

        // 飞行状态
        private bool isDiving = false;
        private float circleAngle = 0f;
        private Vector3 diveStartPosition;

        protected override void Awake()
        {
            base.Awake();
            // 飞行型基础属性设置
            maxHealth = 80f;
            moveSpeed = 4f;
            attackDamage = 8f;
            attackCooldown = 1.5f;
            enemyType = EnemyType.Flying;
        }

        protected override void Start()
        {
            base.Start();
            // 初始化飞行高度
            Vector3 pos = transform.position;
            pos.y = flyHeight;
            transform.position = pos;
        }

        protected override void Update()
        {
            if (isDead) return;

            UpdateState();

            if (isDiving)
            {
                UpdateDiveAttack();
            }
            else
            {
                ExecuteCurrentState();
            }
        }

        protected override void OnPatrol()
        {
            // 在巡逻点上方盘旋
            circleAngle += circleSpeed * Time.deltaTime;

            Vector3 patrolCenter = transform.position;
            patrolCenter.y = 0;

            Vector3 offset = new Vector3(
                Mathf.Cos(circleAngle) * circleRadius,
                0,
                Mathf.Sin(circleAngle) * circleRadius
            );

            Vector3 targetPos = patrolCenter + offset;
            targetPos.y = flyHeight;

            // 平滑移动到目标位置
            transform.position = Vector3.Lerp(transform.position, targetPos, moveSpeed * Time.deltaTime * 0.1f);

            // 朝向移动方向
            if (offset != Vector3.zero)
            {
                transform.rotation = Quaternion.LookRotation(offset);
            }
        }

        protected override void OnChase()
        {
            if (target == null) return;

            // 检查是否可以俯冲攻击
            float horizontalDistance = Vector3.Distance(
                new Vector3(transform.position.x, 0, transform.position.z),
                new Vector3(target.position.x, 0, target.position.z)
            );

            if (horizontalDistance <= diveAttackRange && !isDiving)
            {
                // 俯冲攻击
                StartDiveAttack();
                return;
            }

            // 在目标上方盘旋
            circleAngle += circleSpeed * Time.deltaTime;

            Vector3 targetPos = target.position;
            targetPos.y = flyHeight;

            // 添加盘旋偏移
            Vector3 circleOffset = new Vector3(
                Mathf.Cos(circleAngle) * circleRadius,
                0,
                Mathf.Sin(circleAngle) * circleRadius
            );

            targetPos += circleOffset;

            // 移动到目标位置上方
            transform.position = Vector3.MoveTowards(
                transform.position,
                targetPos,
                moveSpeed * Time.deltaTime
            );

            // 朝向目标
            Vector3 lookDir = target.position - transform.position;
            lookDir.y = 0;
            if (lookDir != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(lookDir),
                    rotationSpeed * Time.deltaTime
                );
            }
        }

        /// <summary>
        /// 开始俯冲攻击
        /// </summary>
        private void StartDiveAttack()
        {
            isDiving = true;
            diveStartPosition = transform.position;

            PlayAnimation("Dive");
            EffectManager.Instance?.PlayEffect("EnemyDive", transform.position);
        }

        /// <summary>
        /// 更新俯冲攻击
        /// </summary>
        private void UpdateDiveAttack()
        {
            if (target == null)
            {
                ReturnToHeight();
                return;
            }

            // 俯冲到目标位置
            Vector3 diveTarget = target.position;
            diveTarget.y = diveHeight;

            transform.position = Vector3.MoveTowards(
                transform.position,
                diveTarget,
                moveSpeed * 3f * Time.deltaTime
            );

            // 朝向俯冲方向
            Vector3 diveDir = (diveTarget - transform.position).normalized;
            transform.rotation = Quaternion.LookRotation(diveDir);

            // 到达俯冲高度或接近目标
            if (transform.position.y <= diveHeight + 0.5f)
            {
                PerformDiveAttack();
            }
        }

        /// <summary>
        /// 执行俯冲攻击
        /// </summary>
        private void PerformDiveAttack()
        {
            // 对周围造成伤害
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, 2f);
            foreach (var hitCollider in hitColliders)
            {
                if (hitCollider.CompareTag("Player"))
                {
                    var playerHealth = hitCollider.GetComponent<PlayerHealth>();
                    if (playerHealth != null)
                    {
                        playerHealth.TakeDamage(diveAttackDamage);
                    }
                }
            }

            // 攻击特效
            EffectManager.Instance?.PlayEffect("DiveImpact", transform.position);

            // 返回飞行高度
            ReturnToHeight();
        }

        /// <summary>
        /// 返回飞行高度
        /// </summary>
        private void ReturnToHeight()
        {
            isDiving = false;
            PlayAnimation("Fly");

            // 向上飞回
            Vector3 returnPos = transform.position;
            returnPos.y = flyHeight;
            transform.position = returnPos;
        }

        protected override void PerformAttack()
        {
            // 飞行敌人使用俯冲攻击替代普通攻击
            // 此方法在OnChase中处理
        }

        protected override void MoveTowards(Vector3 targetPosition)
        {
            // 飞行敌人使用自己的移动逻辑
            Vector3 direction = (targetPosition - transform.position).normalized;
            transform.position += direction * moveSpeed * Time.deltaTime;
        }

        public override void TakeDamage(float damage)
        {
            // 飞行敌人受击时可能坠落
            base.TakeDamage(damage);

            // 血量低于30%时坠落
            if (HealthPercent < 0.3f && transform.position.y > 1f)
            {
                StartFalling();
            }
        }

        /// <summary>
        /// 开始坠落
        /// </summary>
        private void StartFalling()
        {
            // 降低飞行高度
            flyHeight = 1f;
            moveSpeed *= 0.5f;

            PlayAnimation("Fall");
            EffectManager.Instance?.PlayEffect("EnemyFall", transform.position);
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            // 显示飞行高度
            Gizmos.color = Color.cyan;
            Vector3 groundPos = transform.position;
            groundPos.y = 0;
            Gizmos.DrawLine(groundPos, groundPos + Vector3.up * flyHeight);
            Gizmos.DrawWireSphere(groundPos + Vector3.up * flyHeight, 0.5f);

            // 显示俯冲范围
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundPos, diveAttackRange);
        }
    }
}
