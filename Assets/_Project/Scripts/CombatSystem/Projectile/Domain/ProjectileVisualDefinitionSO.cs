using UnityEngine;
using UnityEngine.Serialization;

namespace CombatSystem
{
    /// <summary>
    /// 配置投射物视觉、装备外观覆盖和对象池容量。
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

        /// <summary>该视觉是否覆盖发射器默认弹体外观。</summary>
        public bool OverridesEmitterVisual;

        /// <summary>弹体视觉横向宽度，单位为世界单位。</summary>
        [Min(0f)] public float ProjectileWidth = 0.75f;

        /// <summary>对象池首次预热的实例数量。</summary>
        [FormerlySerializedAs("PrewarmCount")]
        [Min(0)] public int PrewarmInstanceCount;

        /// <summary>对象池最多保留的视觉实例数量。</summary>
        [FormerlySerializedAs("MaximumRetained")]
        [Min(1)] public int MaximumRetainedInstanceCount = 256;
    }
}
