using UnityEngine;

namespace TurretSystem
{
    /// <summary>
    /// 炮台类型定义 ScriptableObject。
    /// 定义炮台的基础属性、发射口、模块插槽和分支升级路径。
    /// 替代旧的 TurretLevelData 单向链表。
    /// </summary>
    [CreateAssetMenu(menuName = "Turret/Turret Base")]
    public class TurretBase : ScriptableObject
    {
        [Header("基本信息")]
        public string turretId;
        public string displayName;
        public GameObject modelPrefab;

        [Header("发射口")]
        public TurretPortConfig[] firingPorts;

        [Header("基础属性")]
        [Tooltip("球体探测半径（所有端口共用）。")]
        public float detectionRadius = 10f;
        public float baseRange = 15f;
        public float baseFireRate = 1f;
        public float baseRotationSpeed = 180f;
        public int baseProjectileCount = 1;

        [Header("球冠")]
        [Tooltip("球冠高度。Turret 位于球冠顶点，port 分布在球冠底面圆周上。运行时会根据当前星球半径自动限制在有效范围内。")]
        [Min(0f)]
        public float capHeight = 1f;

        [Header("炮台背包")]
        [Tooltip("炮台级背包列数。")]
        [Range(1, 8)] public int turretInventoryColumns = 4;
        [Tooltip("炮台级背包行数。")]
        [Range(1, 8)] public int turretInventoryRows = 4;

        /// <summary>获取有效发射口数量。</summary>
        public int ActivePortCount => firingPorts?.Length ?? 0;
    }
}
