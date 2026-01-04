using UnityEngine;
using Utils;

namespace BulletSystem
{
    /// <summary>
    /// 子弹脚本
    /// 处理飞行、碰撞和伤害
    /// </summary>
    public class Bullet : MonoBehaviour
    {
        private BulletConfig config;
        private Transform target;
        private float damage;
        private float traveledDistance;
        private int penetrationUsed;
        private Vector3 lastPosition;
        private ObjectPool<Bullet> pool;

        /// <summary>
        /// 初始化子弹
        /// </summary>
        public void Initialize(BulletConfig bulletConfig, Transform targetTransform, float bulletDamage, ObjectPool<Bullet> bulletPool = null)
        {
            config = bulletConfig;
            target = targetTransform;
            damage = bulletDamage;
            pool = bulletPool;
            lastPosition = transform.position;
            traveledDistance = 0;
            penetrationUsed = 0;

            // 应用配置
            if (config != null)
            {
                if (config.size > 0)
                {
                    transform.localScale = Vector3.one * config.size;
                }

                var renderer = GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = config.bulletColor;
                }
            }
        }

        /// <summary>
        /// 释放回对象池（替代 Destroy）
        /// </summary>
        public void Release()
        {
            if (pool != null)
            {
                pool.Release(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        void Start()
        {
            // 保险超时自动回收
            Invoke(nameof(Release), 5f);
        }

        void Update()
        {
            Move();
            CheckDistance();
        }

        /// <summary>
        /// 移动子弹
        /// </summary>
        private void Move()
        {
            float speed = config != null ? config.speed : 15f;

            if (config != null && config.isHoming && target != null)
            {
                // 追踪模式
                Vector3 direction = (target.position - transform.position).normalized;
                transform.rotation = Quaternion.LookRotation(direction);
                transform.position += direction * speed * Time.deltaTime;
            }
            else
            {
                // 直线飞行
                transform.position += transform.forward * speed * Time.deltaTime;
            }
        }

        /// <summary>
        /// 检查飞行距离
        /// </summary>
        private void CheckDistance()
        {
            float stepDistance = Vector3.Distance(transform.position, lastPosition);
            traveledDistance += stepDistance;
            lastPosition = transform.position;

            float maxDist = config != null ? config.maxDistance : 50f;
            if (traveledDistance >= maxDist)
            {
                Release();
            }
        }

        /// <summary>
        /// 碰撞处理
        /// </summary>
        void OnTriggerEnter(Collider other)
        {
            // 忽略墙壁
            if (config != null && config.ignoreWalls && other.CompareTag("Wall"))
            {
                return;
            }

            // 检查是否击中敌人
            if (other.CompareTag("Enemy"))
            {
                DealDamage(other.gameObject);
                PlayHitEffect();

                // 处理穿透
                int maxPenetration = config != null ? config.penetrationCount : 0;
                if (maxPenetration > 0)
                {
                    penetrationUsed++;
                    if (penetrationUsed >= maxPenetration)
                    {
                        Release();
                    }
                }
                else
                {
                    Release();
                }
            }
            // 击中墙壁
            else if (other.CompareTag("Wall"))
            {
                PlayHitEffect();
                Release();
            }
        }

        /// <summary>
        /// 造成伤害
        /// </summary>
        private void DealDamage(GameObject enemy)
        {
            // 尝试获取敌人脚本
            var enemyHealth = enemy.GetComponent<Enemy.EnemyHealth>();
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage);
            }
            else
            {
                // 通用伤害接口
                enemy.SendMessage("TakeDamage", damage, SendMessageOptions.DontRequireReceiver);
            }
        }

        /// <summary>
        /// 播放击中特效
        /// </summary>
        private void PlayHitEffect()
        {
            if (config != null && config.hitEffectPrefab != null)
            {
                Instantiate(config.hitEffectPrefab, transform.position, Quaternion.identity);
            }
        }

        void OnDestroy()
        {
            CancelInvoke(nameof(Release));
        }
    }
}

namespace Enemy
{
    /// <summary>
    /// 敌人血量接口(示例)
    /// </summary>
    public class EnemyHealth : MonoBehaviour
    {
        public float health = 100f;

        public void TakeDamage(float damage)
        {
            health -= damage;
            if (health <= 0)
            {
                Destroy(gameObject);
            }
        }
    }
}
