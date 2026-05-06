using CameraSystem;
using EffectSystem;
using PlayerSystem;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// 坦克敌人
    /// 高血量、低移速、强力攻击、有护盾
    /// </summary>
    public class EnemyTank : EnemyBase
    {
        [Header("坦克型特性")]
        [Tooltip("护盾值")]
        public float shieldValue = 50f;

        [Tooltip("护盾恢复速度/秒")]
        public float shieldRegenRate = 5f;

        [Tooltip("护盾恢复延迟")]
        public float shieldRegenDelay = 3f;

        [Tooltip("冲锋技能冷却")]
        public float chargeCooldown = 8f;

        [Tooltip("冲锋距离")]
        public float chargeDistance = 10f;

        [Tooltip("冲锋伤害")]
        public float chargeDamage = 30f;

        // 护盾状态
        private float currentShield;
        private float lastDamageTime;
        private bool isCharging;
        private float lastChargeTime;

        // 冲锋状态
        private Vector3 chargeTarget;
        private float chargeTimer;

        protected override void Awake()
        {
            base.Awake();
            // 坦克型基础属性设置
            maxHealth = 300f;
            moveSpeed = 1.5f;
            attackDamage = 20f;
            attackCooldown = 2f;
            enemyType = EnemyType.Tank;
        }

        protected override void Initialize()
        {
            base.Initialize();
            currentShield = shieldValue;
            lastDamageTime = -shieldRegenDelay;
            isCharging = false;
        }

        protected override void Update()
        {
            base.Update();
            UpdateShield();
            UpdateCharge();
        }

        /// <summary>
        /// 更新护盾
        /// </summary>
        private void UpdateShield()
        {
            if (Time.time - lastDamageTime >= shieldRegenDelay && currentShield < shieldValue)
            {
                currentShield += shieldRegenRate * Time.deltaTime;
                currentShield = Mathf.Min(currentShield, shieldValue);
            }
        }

        /// <summary>
        /// 更新冲锋状态
        /// </summary>
        private void UpdateCharge()
        {
            if (!isCharging) return;

            chargeTimer -= Time.deltaTime;

            // 冲锋移动
            Vector3 direction = (chargeTarget - transform.position).normalized;
            direction.y = 0;

            if (direction != Vector3.zero)
            {
                transform.position += direction * moveSpeed * 3f * Time.deltaTime;
                transform.rotation = Quaternion.LookRotation(direction);
            }

            // 检测冲锋结束
            float distanceToTarget = Vector3.Distance(transform.position, chargeTarget);
            if (distanceToTarget < 1f || chargeTimer <= 0)
            {
                EndCharge();
            }
        }

        protected override void OnPatrol()
        {
            // 坦克型敌人在巡逻点缓慢移动
            // 简单的原地停留或缓慢徘徊逻辑
            // 实际项目中可以使用NavMeshAgent或自定义巡逻路径
        }

        protected override void OnChase()
        {
            if (target == null) return;

            // 尝试使用冲锋技能
            if (!isCharging && Time.time - lastChargeTime >= chargeCooldown)
            {
                float distanceToTarget = Vector3.Distance(transform.position, target.position);
                if (distanceToTarget <= chargeDistance && distanceToTarget >= 3f)
                {
                    StartCharge();
                    return;
                }
            }

            // 普通追踪
            if (!isCharging)
            {
                base.OnChase();
            }
        }

        /// <summary>
        /// 开始冲锋
        /// </summary>
        private void StartCharge()
        {
            isCharging = true;
            chargeTarget = target.position;
            chargeTimer = 2f; // 冲锋最长时间
            lastChargeTime = Time.time;

            PlayAnimation("Charge");
            EffectManager.Instance?.PlayEffect("EnemyCharge", transform.position);

            // 冲锋预警
            Debug.Log("坦克敌人准备冲锋!");
        }

        /// <summary>
        /// 结束冲锋
        /// </summary>
        private void EndCharge()
        {
            isCharging = false;
            PlayAnimation("ChargeEnd");

            // 对周围造成伤害
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, 2f);
            foreach (var hitCollider in hitColliders)
            {
                if (hitCollider.CompareTag("Player"))
                {
                    var playerHealth = hitCollider.GetComponent<PlayerHealth>();
                    if (playerHealth != null)
                    {
                        playerHealth.TakeDamage(chargeDamage);
                    }
                }
            }
        }

        protected override void PerformAttack()
        {
            if (target == null) return;

            // 重击 - 对玩家造成大量伤害并击退
            var playerHealth = target.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(attackDamage);

                // 击退效果
                Vector3 knockbackDir = (target.position - transform.position).normalized;
                knockbackDir.y = 0.5f;
                target.GetComponent<Rigidbody>()?.AddForce(knockbackDir * 500f);
            }

            // 震地特效
            EffectManager.Instance?.PlayEffect("GroundSlam", transform.position);

            // 摄像机震动
            CameraShake.Instance?.Shake(0.3f, 0.5f);
        }

        public override void TakeDamage(float damage)
        {
            lastDamageTime = Time.time;

            // 优先扣除护盾
            if (currentShield > 0)
            {
                float shieldAbsorb = Mathf.Min(currentShield, damage);
                currentShield -= shieldAbsorb;
                damage -= shieldAbsorb;

                // 播放护盾受击特效
                EffectManager.Instance?.PlayEffect("ShieldHit", transform.position);

                if (damage <= 0) return; // 护盾完全吸收伤害
            }

            base.TakeDamage(damage);
        }

        protected override void OnDeath()
        {
            base.OnDeath();

            // 坦克死亡时爆炸，对周围造成伤害
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, 5f);
            foreach (var hitCollider in hitColliders)
            {
                if (hitCollider.CompareTag("Player"))
                {
                    var playerHealth = hitCollider.GetComponent<PlayerHealth>();
                    playerHealth?.TakeDamage(attackDamage * 0.5f);
                }
            }

            // 大爆炸特效
            EffectManager.Instance?.PlayEffect("BigExplosion", transform.position);
            CameraShake.Instance?.Shake(0.5f, 1f);
        }

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            // 显示冲锋距离
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireSphere(transform.position, chargeDistance);

            // 显示爆炸范围
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, 5f);
        }
    }
}
