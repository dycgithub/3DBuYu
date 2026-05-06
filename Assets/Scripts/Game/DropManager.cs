using UnityEngine;
using System.Collections.Generic;

namespace GameSystem
{
    /// <summary>
    /// 掉落物品类型
    /// </summary>
    public enum DropItemType
    {
        Coin,       // 金币
        Experience, // 经验球
        HealthPack, // 生命包
        PowerUp,    // 强化道具
        Gem         // 宝石
    }

    /// <summary>
    /// 掉落物品数据
    /// </summary>
    [System.Serializable]
    public class DropItemData
    {
        [Tooltip("掉落类型")]
        public DropItemType type;

        [Tooltip("掉落预制体")]
        public GameObject prefab;

        [Tooltip("掉落权重")]
        public int weight = 1;

        [Tooltip("最小数量")]
        public int minAmount = 1;

        [Tooltip("最大数量")]
        public int maxAmount = 1;

        [Tooltip("拾取范围")]
        public float pickupRange = 1.5f;

        [Tooltip("存在时间（秒，0=永久）")]
        public float lifetime = 30f;
    }

    /// <summary>
    /// 掉落管理器
    /// 管理敌人死亡后的掉落物品生成
    /// </summary>
    public class DropManager : MonoBehaviour
    {
        [Header("掉落配置")]
        [Tooltip("掉落物品列表")]
        public List<DropItemData> dropItems = new List<DropItemData>();

        [Tooltip("金币预制体")]
        public GameObject coinPrefab;

        [Tooltip("经验球预制体")]
        public GameObject experiencePrefab;

        [Tooltip("生命包预制体")]
        public GameObject healthPackPrefab;

        [Tooltip("全局掉落倍数")]
        public float globalDropMultiplier = 1f;

        [Header("生成设置")]
        [Tooltip("掉落散布半径")]
        public float dropSpreadRadius = 2f;

        [Tooltip("掉落弹出高度")]
        public float dropPopHeight = 1f;

        [Tooltip("掉落弹出力度")]
        public float dropPopForce = 3f;

        // 单例
        public static DropManager Instance { get; private set; }

        // 活动中的掉落物品
        private List<DropItem> activeDrops = new List<DropItem>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        /// <summary>
        /// 生成掉落
        /// </summary>
        /// <param name="position">生成位置</param>
        /// <param name="coins">金币数量</param>
        /// <param name="experience">经验值</param>
        /// <param name="dropChance">特殊掉落概率</param>
        public void SpawnDrops(Vector3 position, int coins, int experience, float dropChance = 0.5f)
        {
            // 生成金币
            if (coins > 0 && coinPrefab != null)
            {
                SpawnCoinDrop(position, coins);
            }

            // 生成经验球
            if (experience > 0 && experiencePrefab != null)
            {
                SpawnExperienceDrop(position, experience);
            }

            // 随机特殊掉落
            if (Random.value < dropChance * globalDropMultiplier)
            {
                SpawnRandomDrop(position);
            }
        }

        /// <summary>
        /// 生成金币掉落
        /// </summary>
        private void SpawnCoinDrop(Vector3 position, int amount)
        {
            // 根据金币数量决定生成方式
            if (amount <= 10)
            {
                // 少量金币，单个掉落
                SpawnDropItem(coinPrefab, position, amount);
            }
            else if (amount <= 50)
            {
                // 中等数量，分成几个
                int numDrops = Mathf.Min(amount / 10, 5);
                int perDrop = amount / numDrops;

                for (int i = 0; i < numDrops; i++)
                {
                    Vector3 offset = Random.insideUnitSphere * dropSpreadRadius;
                    offset.y = 0;
                    SpawnDropItem(coinPrefab, position + offset, perDrop);
                }
            }
            else
            {
                // 大量金币，大量散落
                int numDrops = Mathf.Min(amount / 5, 10);
                for (int i = 0; i < numDrops; i++)
                {
                    Vector3 offset = Random.insideUnitSphere * dropSpreadRadius;
                    offset.y = 0;
                    int dropAmount = Random.Range(amount / numDrops / 2, amount / numDrops);
                    SpawnDropItem(coinPrefab, position + offset, dropAmount);
                }
            }
        }

        /// <summary>
        /// 生成经验掉落
        /// </summary>
        private void SpawnExperienceDrop(Vector3 position, int amount)
        {
            // 经验球可以合并成更大的
            if (amount <= 20)
            {
                SpawnDropItem(experiencePrefab, position, amount);
            }
            else
            {
                // 大经验球
                SpawnDropItem(experiencePrefab, position, amount, true);
            }
        }

        /// <summary>
        /// 生成随机掉落
        /// </summary>
        private void SpawnRandomDrop(Vector3 position)
        {
            if (dropItems.Count == 0) return;

            // 按权重随机选择
            int totalWeight = 0;
            foreach (var item in dropItems)
            {
                totalWeight += item.weight;
            }

            int randomWeight = Random.Range(0, totalWeight);
            int currentWeight = 0;

            foreach (var item in dropItems)
            {
                currentWeight += item.weight;
                if (randomWeight < currentWeight)
                {
                    int amount = Random.Range(item.minAmount, item.maxAmount + 1);
                    SpawnDropItem(item.prefab, position, amount);
                    break;
                }
            }
        }

        /// <summary>
        /// 生成单个掉落物品
        /// </summary>
        private DropItem SpawnDropItem(GameObject prefab, Vector3 position, int amount, bool isLarge = false)
        {
            if (prefab == null) return null;

            // 随机位置偏移
            Vector3 spawnPos = position + Random.insideUnitSphere * dropSpreadRadius;
            spawnPos.y = position.y + dropPopHeight;

            // 生成
            GameObject dropObj = Instantiate(prefab, spawnPos, Random.rotation);
            DropItem dropItem = dropObj.GetComponent<DropItem>();

            if (dropItem == null)
            {
                dropItem = dropObj.AddComponent<DropItem>();
            }

            // 初始化
            dropItem.Initialize(amount, isLarge);
            activeDrops.Add(dropItem);

            // 施加弹出力
            Rigidbody rb = dropObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                Vector3 force = Random.onUnitSphere * dropPopForce;
                force.y = Mathf.Abs(force.y);
                rb.AddForce(force, ForceMode.Impulse);
            }

            return dropItem;
        }

        /// <summary>
        /// 注册掉落物品被拾取
        /// </summary>
        public void RegisterPickup(DropItem item)
        {
            if (activeDrops.Contains(item))
            {
                activeDrops.Remove(item);
            }
        }

        /// <summary>
        /// 清除所有掉落
        /// </summary>
        public void ClearAllDrops()
        {
            foreach (var drop in activeDrops.ToArray())
            {
                if (drop != null)
                {
                    Destroy(drop.gameObject);
                }
            }

            activeDrops.Clear();
        }
    }

    /// <summary>
    /// 掉落物品组件
    /// 附加到掉落物品预制体上
    /// </summary>
    public class DropItem : MonoBehaviour
    {
        [Header("设置")]
        [Tooltip("拾取范围")]
        public float pickupRange = 1.5f;

        [Tooltip("自动吸附范围")]
        public float magnetRange = 5f;

        [Tooltip("吸附速度")]
        public float magnetSpeed = 8f;

        [Tooltip("存在时间（秒，0=永久）")]
        public float lifetime = 30f;

        [Tooltip("闪烁开始时间（在消失前多久开始闪烁）")]
        public float blinkStartTime = 5f;

        [Header("效果")]
        [Tooltip("拾取音效")]
        public AudioClip pickupSound;

        [Tooltip("拾取特效")]
        public GameObject pickupEffect;

        // 数据
        private DropItemType itemType;
        private int amount;
        private bool isLarge;
        private float spawnTime;
        private bool isMagnetized = false;
        private Transform target;

        // 组件
        private Renderer itemRenderer;
        private Collider itemCollider;

        public DropItemType ItemType => itemType;
        public int Amount => amount;

        public void Initialize(int amount, bool isLarge = false)
        {
            this.amount = amount;
            this.isLarge = isLarge;
            this.spawnTime = Time.time;

            // 根据数量调整视觉效果
            if (isLarge)
            {
                transform.localScale *= 1.5f;
            }

            itemRenderer = GetComponent<Renderer>();
            itemCollider = GetComponent<Collider>();

            // 启动消失计时
            if (lifetime > 0)
            {
                StartCoroutine(DisappearCoroutine());
            }
        }

        private void Update()
        {
            // 磁力吸附
            if (isMagnetized && target != null)
            {
                Vector3 direction = (target.position - transform.position).normalized;
                transform.position += direction * magnetSpeed * Time.deltaTime;
            }
            else
            {
                // 检测玩家进入磁力范围
                CheckForPlayer();
            }

            // 检测拾取
            CheckPickup();
        }

        /// <summary>
        /// 检测玩家进入磁力范围
        /// </summary>
        private void CheckForPlayer()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, magnetRange);
            foreach (var collider in colliders)
            {
                if (collider.CompareTag("Player"))
                {
                    isMagnetized = true;
                    target = collider.transform;
                    break;
                }
            }
        }

        /// <summary>
        /// 检测是否被拾取
        /// </summary>
        private void CheckPickup()
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, pickupRange);
            foreach (var collider in colliders)
            {
                if (collider.CompareTag("Player"))
                {
                    PickUp(collider.gameObject);
                    break;
                }
            }
        }

        /// <summary>
        /// 拾取
        /// </summary>
        private void PickUp(GameObject player)
        {
            // 给予资源
            switch (itemType)
            {
                case DropItemType.Coin:
                    ResourceManager.Instance?.AddCoins(amount, "拾取");
                    break;
                case DropItemType.Experience:
                    ResourceManager.Instance?.AddExperience(amount, "拾取");
                    break;
                case DropItemType.HealthPack:
                    var health = player.GetComponent<PlayerSystem.PlayerHealth>();
                    health?.Heal(amount);
                    break;
            }

            // 播放效果
            if (pickupSound != null)
            {
                AudioSource.PlayClipAtPoint(pickupSound, transform.position);
            }

            if (pickupEffect != null)
            {
                Instantiate(pickupEffect, transform.position, Quaternion.identity);
            }

            // 通知管理器
            DropManager.Instance?.RegisterPickup(this);

            // 销毁
            Destroy(gameObject);
        }

        /// <summary>
        /// 消失协程
        /// </summary>
        private System.Collections.IEnumerator DisappearCoroutine()
        {
            float elapsed = 0f;

            while (elapsed < lifetime)
            {
                elapsed += Time.deltaTime;

                // 开始闪烁
                if (elapsed >= lifetime - blinkStartTime)
                {
                    float blinkRate = 0.2f;
                    if (itemRenderer != null)
                    {
                        itemRenderer.enabled = !itemRenderer.enabled;
                    }
                    yield return new WaitForSeconds(blinkRate);
                }
                else
                {
                    yield return null;
                }
            }

            // 销毁
            Destroy(gameObject);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, pickupRange);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, magnetRange);
        }
    }
}
