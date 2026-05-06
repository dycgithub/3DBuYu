using System;
using CameraSystem;
using EffectSystem;
using GameSystem;
using UnityEngine;

namespace PlayerSystem
{
    /// <summary>
    /// 玩家血量系统
    /// 管理玩家的生命值、受伤、死亡和复活
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class PlayerHealth : MonoBehaviour
    {
        [Header("基础属性")]
        [Tooltip("最大生命值")]
        [SerializeField]
        private float maxHealth = 100f;

        [Tooltip("初始生命值")]
        [SerializeField]
        private float initialHealth = 100f;

        [Tooltip("无敌时间（受伤后）")]
        [SerializeField]
        private float invincibilityTime = 1f;

        [Tooltip("每秒自然回血")]
        [SerializeField]
        private float healthRegenPerSecond = 0f;

        [Tooltip("回血延迟（受伤后多久开始回血）")]
        [SerializeField]
        private float regenDelay = 5f;

        [Header("视觉效果")]
        [Tooltip("受伤时的屏幕闪烁颜色")]
        [SerializeField]
        private Color damageFlashColor = new Color(1f, 0f, 0f, 0.3f);

        [Tooltip("受伤闪烁持续时间")]
        [SerializeField]
        private float flashDuration = 0.2f;

        [Tooltip("受伤特效预制体")]
        [SerializeField]
        private GameObject damageEffectPrefab;

        [Tooltip("死亡特效预制体")]
        [SerializeField]
        private GameObject deathEffectPrefab;

        [Header("音效")]
        [Tooltip("受伤音效")]
        [SerializeField]
        private AudioClip damageSound;

        [Tooltip("死亡音效")]
        [SerializeField]
        private AudioClip deathSound;

        [Header("调试")]
        [SerializeField]
        private bool showDebugInfo = false;

        // 当前状态
        private float currentHealth;
        private bool isDead = false;
        private bool isInvincible = false;
        private float invincibilityTimer;
        private float lastDamageTime = -999f;

        // 组件缓存
        private Collider playerCollider;
        private Rigidbody playerRigidbody;

        #region 属性

        /// <summary>
        /// 当前生命值
        /// </summary>
        public float CurrentHealth => currentHealth;

        /// <summary>
        /// 最大生命值
        /// </summary>
        public float MaxHealth => maxHealth;

        /// <summary>
        /// 生命值百分比 (0-1)
        /// </summary>
        public float HealthPercent => currentHealth / maxHealth;

        /// <summary>
        /// 是否死亡
        /// </summary>
        public bool IsDead => isDead;

        /// <summary>
        /// 是否无敌
        /// </summary>
        public bool IsInvincible => isInvincible;

        #endregion

        #region 事件

        /// <summary>
        /// 生命值改变事件 (当前值, 最大值)
        /// </summary>
        public event Action<float, float> OnHealthChanged;

        /// <summary>
        /// 受到伤害事件 (伤害值, 剩余血量)
        /// </summary>
        public event Action<float, float> OnDamageTaken;

        /// <summary>
        /// 治疗事件 (治疗量, 当前血量)
        /// </summary>
        public event Action<float, float> OnHealed;

        /// <summary>
        /// 死亡事件
        /// </summary>
        public event Action OnDeath;

        /// <summary>
        /// 复活事件
        /// </summary>
        public event Action OnRespawn;

        /// <summary>
        /// 无敌状态改变事件 (是否无敌)
        /// </summary>
        public event Action<bool> OnInvincibilityChanged;

        #endregion

        #region Unity生命周期

        private void Awake()
        {
            playerCollider = GetComponent<Collider>();
            playerRigidbody = GetComponent<Rigidbody>();
        }

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            if (isDead) return;

            UpdateInvincibility();
            UpdateHealthRegen();
        }

        #endregion

        #region 初始化

        /// <summary>
        /// 初始化血量
        /// </summary>
        private void Initialize()
        {
            currentHealth = initialHealth;
            isDead = false;
            isInvincible = false;
            invincibilityTimer = 0f;

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        #endregion

        #region 受伤与治疗

        /// <summary>
        /// 受到伤害
        /// </summary>
        /// <param name="damage">伤害值</param>
        /// <returns>是否成功造成伤害</returns>
        public bool TakeDamage(float damage)
        {
            if (isDead || isInvincible || damage <= 0) return false;

            currentHealth -= damage;
            currentHealth = Mathf.Max(currentHealth, 0f);
            lastDamageTime = Time.time;

            if (showDebugInfo)
            {
                Debug.Log($"玩家受到伤害: {damage}, 剩余血量: {currentHealth}");
            }

            // 触发事件
            OnDamageTaken?.Invoke(damage, currentHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            // 播放效果
            PlayDamageEffects();

            // 检查死亡
            if (currentHealth <= 0)
            {
                Die();
            }
            else
            {
                // 启动无敌时间
                StartInvincibility();
            }

            return true;
        }

        /// <summary>
        /// 治疗
        /// </summary>
        /// <param name="amount">治疗量</param>
        /// <returns>实际治疗量</returns>
        public float Heal(float amount)
        {
            if (isDead || amount <= 0 || currentHealth >= maxHealth) return 0f;

            float actualHeal = Mathf.Min(amount, maxHealth - currentHealth);
            currentHealth += actualHeal;

            if (showDebugInfo)
            {
                Debug.Log($"玩家恢复生命: {actualHeal}, 当前血量: {currentHealth}");
            }

            OnHealed?.Invoke(actualHeal, currentHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            return actualHeal;
        }

        /// <summary>
        /// 立即回复全部生命
        /// </summary>
        public void FullHeal()
        {
            Heal(maxHealth);
        }

        #endregion

        #region 死亡与复活

        /// <summary>
        /// 死亡
        /// </summary>
        private void Die()
        {
            if (isDead) return;

            isDead = true;
            currentHealth = 0f;

            if (showDebugInfo)
            {
                Debug.Log("玩家死亡！");
            }

            // 禁用碰撞器
            if (playerCollider != null)
                playerCollider.enabled = false;

            // 播放死亡效果
            PlayDeathEffects();

            // 触发事件
            OnDeath?.Invoke();

            // 通知游戏管理器
            GameManager.Instance?.OnPlayerDeath();
        }

        /// <summary>
        /// 复活
        /// </summary>
        /// <param name="respawnPosition">复活位置</param>
        /// <param name="healAmount">复活时恢复的生命值（-1为全部恢复）</param>
        public void Respawn(Vector3 respawnPosition, float healAmount = -1)
        {
            if (!isDead)
            {
                Debug.LogWarning("玩家未死亡，无法复活");
                return;
            }

            // 重置状态
            isDead = false;
            isInvincible = false;
            invincibilityTimer = 0f;

            // 恢复生命
            if (healAmount < 0)
            {
                FullHeal();
            }
            else
            {
                currentHealth = 0f;
                Heal(healAmount);
            }

            // 移动到复活点
            transform.position = respawnPosition;

            // 启用碰撞器
            if (playerCollider != null)
                playerCollider.enabled = true;

            // 清除刚体速度
            if (playerRigidbody != null)
            {
                playerRigidbody.velocity = Vector3.zero;
                playerRigidbody.angularVelocity = Vector3.zero;
            }

            if (showDebugInfo)
            {
                Debug.Log($"玩家复活于: {respawnPosition}");
            }

            // 触发事件
            OnRespawn?.Invoke();

            // 启动复活无敌
            StartInvincibility(3f);
        }

        #endregion

        #region 无敌状态

        /// <summary>
        /// 启动无敌时间
        /// </summary>
        /// <param name="duration">无敌持续时间（默认使用配置值）</param>
        private void StartInvincibility(float duration = -1f)
        {
            float actualDuration = duration > 0 ? duration : invincibilityTime;

            isInvincible = true;
            invincibilityTimer = actualDuration;

            OnInvincibilityChanged?.Invoke(true);

            // 视觉反馈 - 闪烁
            StartCoroutine(InvincibilityFlashCoroutine(actualDuration));
        }

        /// <summary>
        /// 更新无敌状态
        /// </summary>
        private void UpdateInvincibility()
        {
            if (!isInvincible) return;

            invincibilityTimer -= Time.deltaTime;
            if (invincibilityTimer <= 0)
            {
                isInvincible = false;
                OnInvincibilityChanged?.Invoke(false);
            }
        }

        /// <summary>
        /// 无敌闪烁协程
        /// </summary>
        private System.Collections.IEnumerator InvincibilityFlashCoroutine(float duration)
        {
            // 这里可以控制材质透明度或启用/禁用渲染器来实现闪烁效果
            float elapsed = 0f;
            float flashInterval = 0.1f;

            Renderer[] renderers = GetComponentsInChildren<Renderer>();

            while (elapsed < duration)
            {
                // 切换可见性
                foreach (var rend in renderers)
                {
                    rend.enabled = !rend.enabled;
                }

                yield return new WaitForSeconds(flashInterval);
                elapsed += flashInterval;
            }

            // 确保最终可见
            foreach (var rend in renderers)
            {
                rend.enabled = true;
            }
        }

        #endregion

        #region 自然回血

        /// <summary>
        /// 更新自然回血
        /// </summary>
        private void UpdateHealthRegen()
        {
            if (healthRegenPerSecond <= 0) return;
            if (currentHealth >= maxHealth) return;
            if (Time.time - lastDamageTime < regenDelay) return;

            Heal(healthRegenPerSecond * Time.deltaTime);
        }

        #endregion

        #region 属性修改

        /// <summary>
        /// 设置最大生命值
        /// </summary>
        public void SetMaxHealth(float newMaxHealth, bool keepPercent = true)
        {
            if (keepPercent)
            {
                float percent = HealthPercent;
                maxHealth = newMaxHealth;
                currentHealth = maxHealth * percent;
            }
            else
            {
                maxHealth = newMaxHealth;
                currentHealth = Mathf.Min(currentHealth, maxHealth);
            }

            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        /// <summary>
        /// 增加最大生命值
        /// </summary>
        public void IncreaseMaxHealth(float amount, bool heal = true)
        {
            maxHealth += amount;
            if (heal)
            {
                Heal(amount);
            }
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        #endregion

        #region 效果播放

        /// <summary>
        /// 播放受伤效果
        /// </summary>
        private void PlayDamageEffects()
        {
            // 摄像机震动
            CameraShake.Instance?.Shake(0.2f, 0.3f);

            // 播放受伤特效
            if (damageEffectPrefab != null)
            {
                Instantiate(damageEffectPrefab, transform.position, Quaternion.identity);
            }

            // 播放音效
            if (damageSound != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(damageSound);
            }

            // 通过特效管理器播放
            EffectManager.Instance?.PlayEffect("PlayerDamage", transform.position);
        }

        /// <summary>
        /// 播放死亡效果
        /// </summary>
        private void PlayDeathEffects()
        {
            // 播放死亡特效
            if (deathEffectPrefab != null)
            {
                Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
            }

            // 播放音效
            if (deathSound != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(deathSound);
            }

            // 摄像机强烈震动
            CameraShake.Instance?.Shake(0.5f, 0.8f);

            // 通过特效管理器播放
            EffectManager.Instance?.PlayEffect("PlayerDeath", transform.position);
        }

        #endregion

        #region 调试

        [ContextMenu("测试受伤")]
        private void TestDamage()
        {
            TakeDamage(20f);
        }

        [ContextMenu("测试治疗")]
        private void TestHeal()
        {
            Heal(20f);
        }

        [ContextMenu("测试死亡")]
        private void TestDeath()
        {
            TakeDamage(9999f);
        }

        [ContextMenu("测试复活")]
        private void TestRespawn()
        {
            if (isDead)
            {
                Respawn(transform.position + Vector3.up * 2f);
            }
        }

        #endregion
    }
}
