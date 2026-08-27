using UnityEngine;


namespace Play
{
    /// <summary>
    /// 炮台类型定义 ScriptableObject。
    /// 定义炮台的基础属性、发射口。
    /// </summary>
    [CreateAssetMenu(menuName = "Play/Central Data SO")]
    public class CentralSO : ScriptableObject
    {
        [Header("基本信息")]
        public string centralId;
        public string displayName;
        public GameObject modelPrefab;

        [Header("发射器")]
        public TransmitterSO[] Transmitters;

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

        /// <summary>获取有效发射口数量。</summary>
        public int ActivePortCount => Transmitters?.Length ?? 0;
    }
}
