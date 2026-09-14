using UnityEngine;
using UnityEngine.Serialization;

namespace CombatSystem
{
    /// <summary>
    /// 配置投射物视觉预制体及其对象池容量。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Attack Projectile Visual")]
    public sealed class ProjectileVisualDefinitionSO : ScriptableObject
    {
        /// <summary>投射物视觉预制体。</summary>
        [FormerlySerializedAs("Prefab")]
        public GameObject ProjectilePrefab;

        /// <summary>视觉对象使用的颜色色调。</summary>
        [FormerlySerializedAs("Color")]
        public Color TintColor = Color.yellow;

        /// <summary>对象池首次预热的实例数量。</summary>
        [FormerlySerializedAs("PrewarmCount")]
        [Min(0)] public int PrewarmInstanceCount;

        /// <summary>对象池最多保留的视觉实例数量。</summary>
        [FormerlySerializedAs("MaximumRetained")]
        [Min(1)] public int MaximumRetainedInstanceCount = 256;
    }
}
