
using EffectSystem;
using PlayerSystem;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// 普通敌人
    /// 平衡的血量和速度，标准的行为模式
    /// </summary>
    public class EnemyNormal : EnemyBase
    {
        [Header("巡逻设置")]
        [Tooltip("巡逻范围")]
        public float patrolRadius = 5f;

        [Tooltip("巡逻点切换时间")]
        public float patrolPointChangeTime = 3f;

        // 巡逻状态
        private Vector3 patrolCenter;
        private Vector3 currentPatrolTarget;
        private float patrolTimer;

        protected override void Start()
        {
            base.Start();
            patrolCenter = transform.position;
            SetNewPatrolTarget();
        }

        /// <summary>
        /// 设置新的巡逻目标点
        /// </summary>
        private void SetNewPatrolTarget()
        {
            Vector2 randomCircle = Random.insideUnitCircle * patrolRadius;
            currentPatrolTarget = patrolCenter + new Vector3(randomCircle.x, 0, randomCircle.y);
            patrolTimer = 0f;
        }

        protected override void OnPatrol()
        {
            patrolTimer += Time.deltaTime;

            // 到达巡逻点或超时，切换目标
            float distanceToTarget = Vector3.Distance(transform.position, currentPatrolTarget);
            if (distanceToTarget < 0.5f || patrolTimer >= patrolPointChangeTime)
            {
                SetNewPatrolTarget();
            }

            // 向巡逻点移动
            MoveTowards(currentPatrolTarget);
            RotateTowards(currentPatrolTarget);
        }

        protected override void PerformAttack()
        {
            // 近战攻击
            if (target != null)
            {
                // 对玩家造成伤害
                var playerHealth = target.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(attackDamage);
                }
                else
                {
                    // 发送消息作为备选方案
                    target.SendMessage("TakeDamage", attackDamage, SendMessageOptions.DontRequireReceiver);
                }

                // 播放攻击特效
                EffectManager.Instance?.PlayEffect("EnemyAttack", transform.position + transform.forward);
            }
        }
    }
}
