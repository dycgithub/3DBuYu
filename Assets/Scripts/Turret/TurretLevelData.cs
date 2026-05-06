using BulletSystem;
using UnityEngine;

namespace TurretSystem
{
    /// <summary>
    /// 炮台等级数据配置，通过ScriptableObject存储
    /// </summary>
    [CreateAssetMenu(fileName = "TurretLevelData", menuName = "Turret/Level Data")]
    public class TurretLevelData : ScriptableObject
    {
        [Header("基础属性")]
        [Tooltip("等级")]
        public int level = 1;

        [Tooltip("伤害")]
        public float damage = 10f;

        [Tooltip("攻击范围")]
        public float range = 5f;

        [Tooltip("攻击间隔(秒)")]
        public float fireRate = 1f;

        [Tooltip("子弹速度")]
        public float bulletSpeed = 10f;

        [Header("升级属性")]
        [Tooltip("升级所需经验/金币")]
        public int upgradeCost = 100;

        [Tooltip("下一等级数据(可选)")]
        public TurretLevelData nextLevel;

        [Header("视觉效果")]
        [Tooltip("攻击特效名称")]
        public string attackEffectName;

        [Tooltip("攻击特效预制体")]
        public GameObject attackEffectPrefab;

        [Tooltip("升级特效名称")]
        public string upgradeEffectName;

        [Tooltip("升级特效预制体")]
        public GameObject upgradeEffectPrefab;

        [Header("子弹配置引用")]
        [Tooltip("子弹配置")]
        public BulletConfig bulletConfig;

        /// <summary>
        /// 获取完整描述
        /// </summary>
        public string GetDescription()
        {
            return $"等级 {level} | 伤害 {damage:F1} | 范围 {range:F1} | 射速 {1f/fireRate:F1}/秒";
        }

        /// <summary>
        /// 是否可以升级
        /// </summary>
        public bool CanUpgrade => nextLevel != null;
    }
}
