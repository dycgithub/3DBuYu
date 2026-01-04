using UnityEngine;

namespace BulletSystem
{
    /// <summary>
    /// 子弹配置，通过ScriptableObject存储可配置参数
    /// </summary>
    [CreateAssetMenu(fileName = "BulletConfig", menuName = "Turret/Bullet Config")]
    public class BulletConfig : ScriptableObject
    {
        [Header("基础属性")]
        [Tooltip("子弹预制体")]
        public GameObject bulletPrefab;

        [Tooltip("子弹大小")]
        public float size = 0.3f;

        [Tooltip("伤害")]
        public float damage = 10f;

        [Tooltip("飞行速度")]
        public float speed = 15f;

        [Tooltip("最大飞行距离")]
        public float maxDistance = 50f;

        [Tooltip("是否追踪敌人")]
        public bool isHoming = false;

        [Tooltip("追踪转向速度(仅追踪模式)")]
        public float homingTurnSpeed = 10f;

        [Header("视觉效果")]
        [Tooltip("拖尾特效预制体")]
        public GameObject trailEffectPrefab;

        [Tooltip("击中特效预制体")]
        public GameObject hitEffectPrefab;

        [Tooltip("子弹材质颜色")]
        public Color bulletColor = Color.yellow;

        [Header("物理属性")]
        [Tooltip("穿透次数(0=不穿透)")]
        public int penetrationCount = 0;

        [Tooltip("是否忽略墙壁")]
        public bool ignoreWalls = false;

        /// <summary>
        /// 获取完整描述
        /// </summary>
        public string GetDescription()
        {
            string desc = $"伤害 {damage:F1} | 速度 {speed:F1} | 大小 {size:F2}";
            if (isHoming)
                desc += " | 追踪";
            if (penetrationCount > 0)
                desc += $" | 穿透 {penetrationCount}次";
            return desc;
        }
    }
}
