using UnityEngine;
using System.Collections.Generic;
using BulletSystem;
using EffectSystem;
using Utils;

namespace TurretSystem
{
    /// <summary>
    /// 炮台主脚本
    /// 负责搜索最近敌人、瞄准和攻击
    /// </summary>
    public class Turret : MonoBehaviour
    {
        [Header("数据配置")] [Tooltip("初始等级数据")] public TurretLevelData levelData;

        [Header("炮塔引用")] [Tooltip("炮塔模型(用于旋转)")]
        public Transform turretHead;

        [Tooltip("子弹生成点")] public Transform firePoint;

        [Header("对象池配置")] [Tooltip("子弹预热数量")] public int bulletPrewarmCount = 10;

        [Tooltip("对象池最大容量")] public int poolMaxSize = 50;

        [Header("调试")] [Tooltip("是否显示攻击范围")] public bool showGizmos = true;

        // 当前状态
        private int currentLevel = 1;
        private float lastFireTime;
        private Transform target;
        private List<Transform> enemyList = new List<Transform>();
        private ObjectPool<Bullet> bulletPool;

        // 属性代理
        public float Damage => levelData != null ? levelData.damage : 10f;
        public float Range => levelData != null ? levelData.range : 5f;
        public float FireRate => levelData != null ? levelData.fireRate : 1f;
        public float BulletSpeed => levelData != null ? levelData.bulletSpeed : 10f;
        public int CurrentLevel => currentLevel;

        void Start()
        {
            Initialize();
        }

        void Initialize()
        {
            if (levelData == null)
            {
                Debug.LogWarning($"{name}: 未配置等级数据!", this);
                return;
            }

            currentLevel = levelData.level;
            lastFireTime = -FireRate; // 立即可以攻击

            // 初始化子弹对象池
            if (levelData.bulletConfig != null && levelData.bulletConfig.bulletPrefab != null)
            {
                // 从预制体中获取Bullet组件作为模板
                Bullet bulletTemplate = levelData.bulletConfig.bulletPrefab.GetComponent<Bullet>();
                if (bulletTemplate != null)
                {
                    bulletPool = new ObjectPool<Bullet>(
                        bulletTemplate, // 使用Bullet组件作为模板
                        transform,
                        bulletPrewarmCount,
                        poolMaxSize
                    );
                }
                else
                {
                    Debug.LogError($"子弹预制体 {levelData.bulletConfig.bulletPrefab.name} 缺少Bullet组件!",
                        levelData.bulletConfig.bulletPrefab);
                }
            }
        }


        void Update()
        {
            if (levelData == null) return;

            FindNearestTarget();
            RotateTowardsTarget();
            TryFire();
        }

        /// <summary>
        /// 搜索并锁定最近的敌人
        /// </summary>
        private void FindNearestTarget()
        {
            enemyList.Clear();

            // 获取所有敌人(通过标签)
            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
            foreach (GameObject enemy in enemies)
            {
                if (enemy == null) continue;

                float distance = Vector3.Distance(transform.position, enemy.transform.position);
                if (distance <= Range)
                {
                    enemyList.Add(enemy.transform);
                }
            }

            // 找最近的敌人
            Transform nearest = null;
            float nearestDistance = float.MaxValue;

            foreach (Transform enemy in enemyList)
            {
                if (enemy == null) continue;

                float distance = Vector3.Distance(transform.position, enemy.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = enemy;
                }
            }

            target = nearest;
        }

        /// <summary>
        /// 炮塔旋转朝向目标
        /// </summary>
        private void RotateTowardsTarget()
        {
            if (target == null || turretHead == null) return;

            Vector3 direction = target.position - turretHead.position;
            direction.y = 0; // 保持水平旋转

            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                turretHead.rotation = Quaternion.Slerp(
                    turretHead.rotation,
                    targetRotation,
                    Time.deltaTime * 10f
                );
            }
        }

        /// <summary>
        /// 尝试开火
        /// </summary>
        private void TryFire()
        {
            if (target == null) return;

            float timeSinceLastFire = Time.time - lastFireTime;
            if (timeSinceLastFire >= FireRate)
            {
                Fire();
                lastFireTime = Time.time;
            }
        }

        /// <summary>
        /// 开火
        /// </summary>
        private void Fire()
        {
            if (firePoint == null) firePoint = transform;

            // 获取子弹配置
            BulletConfig bulletConfig = levelData?.bulletConfig;

            // 播放攻击特效
            PlayAttackEffect();

            // 从对象池获取子弹
            Bullet bullet;
            if (bulletPool != null)
            {
                bullet = bulletPool.Get();
                bullet.transform.position = firePoint.position;
                bullet.transform.rotation = firePoint.rotation;
                bullet.Initialize(bulletConfig, target, Damage, bulletPool);
            }
            else if (bulletConfig != null && bulletConfig.bulletPrefab != null)
            {
                // 降级：没有对象池时使用 Instantiate
                GameObject bulletObj = Instantiate(bulletConfig.bulletPrefab, firePoint.position, firePoint.rotation);
                bullet = bulletObj.GetComponent<Bullet>();
                bullet?.Initialize(bulletConfig, target, Damage);
            }
            else
            {
                // 默认子弹（无池）
                GameObject bulletObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                bulletObj.transform.position = firePoint.position;
                bulletObj.transform.rotation = firePoint.rotation;
                bulletObj.transform.localScale = Vector3.one * 0.3f;
                bullet = bulletObj.AddComponent<Bullet>();
                bullet.Initialize(null, target, Damage);
            }
        }

        /// <summary>
        /// 播放攻击特效
        /// </summary>
        private void PlayAttackEffect()
        {
            if (levelData == null) return;

            // 优先使用预制体
            if (levelData.attackEffectPrefab != null)
            {
                Instantiate(levelData.attackEffectPrefab, firePoint != null ? firePoint.position : transform.position,
                    Quaternion.identity);
            }
            // 其次尝试通过名称查找特效
            else if (!string.IsNullOrEmpty(levelData.attackEffectName))
            {
                var effectManager = FindObjectOfType<EffectManager>();
                if (effectManager != null)
                {
                    effectManager.PlayEffect(levelData.attackEffectName,
                        firePoint != null ? firePoint.position : transform.position);
                }
            }
        }

        /// <summary>
        /// 升级炮台
        /// </summary>
        public bool Upgrade()
        {
            if (levelData == null || !levelData.CanUpgrade)
            {
                PlayUpgradeFailedEffect();
                return false;
            }

            // 检查资源(可根据游戏系统扩展)
            // if (GameManager.Instance.SpendGold(levelData.upgradeCost)) { ... }

            // 升级
            TurretLevelData newData = levelData.nextLevel;
            levelData = newData;
            currentLevel = newData.level;

            // 播放升级特效
            PlayUpgradeSuccessEffect();

            // 更新炮塔模型(如果配置了不同模型)
            UpdateTurretModel();

            return true;
        }

        /// <summary>
        /// 播放升级成功特效
        /// </summary>
        private void PlayUpgradeSuccessEffect()
        {
            if (levelData?.upgradeEffectPrefab != null)
            {
                Instantiate(levelData.upgradeEffectPrefab, transform.position, Quaternion.identity);
            }
            else if (!string.IsNullOrEmpty(levelData?.upgradeEffectName))
            {
                var effectManager = FindObjectOfType<EffectManager>();
                if (effectManager != null)
                {
                    effectManager.PlayEffect(levelData.upgradeEffectName, transform.position);
                }
            }
        }

        /// <summary>
        /// 播放升级失败特效
        /// </summary>
        private void PlayUpgradeFailedEffect()
        {
            var effectManager = FindObjectOfType<EffectManager>();
            if (effectManager != null)
            {
                effectManager.PlayEffect("UpgradeFailed", transform.position);
            }
        }

        /// <summary>
        /// 更新炮塔模型(可扩展)
        /// </summary>
        private void UpdateTurretModel()
        {
            // 可根据等级加载不同模型
        }

        /// <summary>
        /// 设置炮塔模型引用
        /// </summary>
        public void SetTurretHead(Transform head)
        {
            turretHead = head;
        }

        /// <summary>
        /// 设置开火点
        /// </summary>
        public void SetFirePoint(Transform point)
        {
            firePoint = point;
        }

        /// <summary>
        /// 设置等级数据
        /// </summary>
        public void SetLevelData(TurretLevelData data)
        {
            levelData = data;
            Initialize();
        }

        /// <summary>
        /// 刷新配置 - 在热更新后重新初始化
        /// </summary>
        public void RefreshConfiguration()
        {
            // 重新初始化以应用新的配置值
            Initialize();

            // 如果正在运行，确保状态正确
            if (levelData != null)
            {
                currentLevel = levelData.level;
            }

            Debug.Log($"[Turret] Configuration refreshed on {gameObject.name}");
        }

        void OnDrawGizmosSelected()
        {
            if (!showGizmos) return;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, Range);

            if (target != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, target.position);
            }
        }
    }
}