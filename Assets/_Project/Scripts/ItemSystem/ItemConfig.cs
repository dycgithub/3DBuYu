using UnityEngine;

namespace ItemSystem
{
    /// <summary>
    /// 物品配置(ScriptableObject):统一描述一件物品的身份信息、经济数值、耐久损耗、
    /// 槽位兼容规则与战斗属性(弹药加成/技能加成)。
    /// 由 <see cref="ItemConfigRegistry"/> 按 <c>itemId</c> 字符串索引,运行时通过配置引用解析。
    /// </summary>
    [CreateAssetMenu(menuName = "Inventory/Item Config")]
    public class ItemConfig : ScriptableObject
    {
        // 四个可序列化子结构,按职责拆分便于维护与序列化
        [SerializeField] private ItemIdentity _identity = new();          // 身份信息:ID / 名称 / 描述
        [SerializeField] private ItemDurabilityData _durability = new();  // 耐久损耗:最大使用时长 / 耐久 / 是否损坏销毁

        [SerializeField] private ItemType _itemType;
        [SerializeField] private ItemShape _shape;                       // 网格形状(放置算法核心数据,不能归 UI 层)
        
        /// <summary>弹药类物品的战斗加成(随装备生效)</summary>
        [Header("Ammunition Stats")]
        public float attackBonus;                  // 攻击加成
        public float rangeBonus;                   // 射程加成
        [Range(0f, 0.5f)] public float criticalChanceBonus; // 暴击率加成(上限 50%)
        public float criticalDamageBonus;          // 暴击伤害加成
        public bool isBounce;                      // 是否可弹射
        public int bounceCount;                    // 弹射次数

        /// <summary>技能类物品的攻速/火力加成(随装备生效)</summary>
        [Header("Skill Stats")]
        public float attackSpeedBonus;             // 攻击速度加成
        public float fireRateModifier = 1f;        // 射速倍率(默认 1,不变)
        public int projectileCountModifier;        // 投射物数量修正(附加子弹数)

        /// <summary>主动技能标识(玩家主动释放的具体技能,None = 纯被动加成)</summary>
        [Header("Active Skill")]
        public SkillKind skillKind = SkillKind.None;
        public float cooldownSeconds = 5f;

        /// <summary>物品提供的子弹配置,决定发射的弹种</summary>
        public ShootingSystem.BulletProfile providedBulletConfig;

        /// <summary>身份信息(只读)</summary>
        public ItemIdentity Identity => _identity;
        
        /// <summary>耐久损耗(只读)</summary>
        public ItemDurabilityData Durability => _durability;


        // ---- 以下为转发到子结构的便捷属性,供序列化与外部直接读写 ----

        public string itemId { get => _identity.itemId; set => _identity.itemId = value; }       // 唯一标识(ID 同时也是存档 key)
        public string displayName { get => _identity.displayName; set => _identity.displayName = value; } // 显示名
        public string description { get => _identity.description; set => _identity.description = value; } // 描述文本
        public float maxUsageTime { get => _durability.maxUsageTime; set => _durability.maxUsageTime = value; } // 最大使用时长
        public int maxDurability { get => _durability.maxDurability; set => _durability.maxDurability = value; } // 最大耐久
        public bool destroyOnBreak { get => _durability.destroyOnBreak; set => _durability.destroyOnBreak = value; } // 耐久耗尽后是否销毁

        public ItemType ItemType { get => _itemType; }

        /// <summary>网格形状(占格定义)。放置/拖拽/吸附共用,属于逻辑数据。</summary>
        public ItemShape Shape { get => _shape; }
        public ItemShape shape { get => _shape; }


    }

    /// <summary>
    /// 主动技能种类 — 决定物品装配到炮塔后生成的具体技能命令(命令模式:ConcreteCommand)。
    /// </summary>
    public enum SkillKind
    {
        /// <summary>非主动技能(纯被动属性加成)。</summary>
        None = 0,
        /// <summary>清屏:瞬间消灭场上所有敌人。</summary>
        KillAllEnemies = 1,
        /// <summary>解锁所有炮口插槽。</summary>
        UnlockAllPorts = 2
    }

    /// <summary>
    /// 单条属性行:属性名 / 数值 / 是否为百分比。
    /// 用于 UI 属性展示(如炮塔/炮口属性面板)。
    /// </summary>
    [System.Serializable]
    public struct StatLine
    {
        public string statName;   // 属性名
        public float value;       // 属性数值
        public bool isPercentage; // 是否为百分比显示

        public StatLine(string name, float val, bool isPct = false)
        {
            statName = name;
            value = val;
            isPercentage = isPct;
        }
    }

}
