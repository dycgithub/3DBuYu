using UnityEngine;
using CombatSystem;
using EffectSystem;
using UnityEngine.Serialization;

namespace Play
{
    /// <summary>
    /// 炮台发射口配置 ScriptableObject。
    /// 每个发射口独立配置基础攻击数值、瞄准锁定、模型和默认投射物。
    /// 多个发射口 = 多管炮台，可独立使用不同的基础值和投射物。
    /// </summary>
    [CreateAssetMenu(menuName = "Play/Port")]
    public class TransmitterSO : ScriptableObject
    {
        /// <summary>发射器端口在配置界面中的名称。</summary>
        [Header("基本信息")]
        public string portName = "主炮口";

        [Tooltip("跨场景和背包绑定使用的稳定端口标识。")]
        public string portId;

        /// <summary>端口跟踪目标的旋转速度，单位为度/秒。</summary>
        [Header("瞄准")]
        [Tooltip("端口跟踪目标的旋转速度（度/秒）。")]
        [Range(1f, 60f)]
        public float trackingSpeed = 10f;

        [Header("基础攻击")]
        [Tooltip("一次攻击的基础伤害。")]
        [Min(0f)]
        public float baseDamage = 10f;

        [Tooltip("单个投射物允许的基础最大距离，单位为世界单位。")]
        [Min(0f)]
        public float baseRange = 50f;

        [Tooltip("端口每秒允许提交的基础攻击次数。")]
        [Min(0.001f)]
        public float attacksPerSecond = 2f;

        [Tooltip("一次攻击生成的基础投射物数量。")]
        [Min(1)]
        public int projectileCount = 1;

        [Tooltip("基础暴击概率，范围为 0 到 1。")]
        [Range(0f, 1f)]
        public float criticalChance = 0.05f;

        [Tooltip("基础暴击伤害倍率。")]
        [Min(1f)]
        public float criticalDamage = 1.5f;

        [Tooltip("一次攻击的基础穿透次数。")]
        [Min(0)]
        public int penetration;

        /// <summary>端口是否在初始化时处于锁定状态。</summary>
        [Header("锁定状态")]
        [Tooltip("初始是否锁定（需 PortExpander 道具解锁）。锁定端口不可开火、不可瞄准。")]
        public bool isInitiallyLocked = false;

        /// <summary>端口解锁时使用的可视化模型预制体。</summary>
        [Header("模型")]
        [Tooltip("发射口可视化模型预制体（可选）。为空则不显示模型。")]
        public GameObject portModelPrefab;

        /// <summary>端口锁定时使用的可视化模型预制体。</summary>
        [Tooltip("锁定状态下使用的模型预制体（可选，为空则使用 portModelPrefab 或隐藏）。")]
        public GameObject lockedModelPrefab;

        /// <summary>背包未提供覆盖配置时使用的默认投射物。</summary>
        [Header("默认投射物")]
        [Tooltip("当背包未装备对应武器时使用的默认投射物配置。")]
        [FormerlySerializedAs("defaultBullet")]
        public ProjectileProfileSO defaultProjectileProfile;

        [Header("发射表现")]
        [Tooltip("该发射口成功提交攻击时播放的默认音效。")]
        public AudioClip fireSound;

        [Tooltip("该发射口成功提交攻击时播放的特效。None 表示不播放。")]
        public EffectId fireEffectId = EffectId.TransmitterFire;
    }
}
