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

        /// <summary>批量发射时相邻弹体中心距的视觉下限，单位为世界单位。</summary>
        [Min(0f)] public float ProjectileWidth = 0.75f;

        /// <summary>只缩放弹体视觉子节点的长度，保持根碰撞体不变。</summary>
        [Min(0.1f)] public float ProjectileLengthMultiplier = 1f;

        /// <summary>只缩放弹体视觉子节点的横向粗细，保持根碰撞体不变。</summary>
        [Min(0.1f)] public float ProjectileThicknessMultiplier = 1f;

        /// <summary>弹尾存留时间；零表示不发射拖尾。</summary>
        [Min(0f)] public float TrailTimeSeconds;

        /// <summary>弹尾起始宽度，单位为世界单位。</summary>
        [Min(0f)] public float TrailWidth = 0.04f;

        /// <summary>拖尾颜色；尾端会淡出为透明。</summary>
        public Color TrailColor = Color.white;

        /// <summary>对象池首次预热的实例数量。</summary>
        [FormerlySerializedAs("PrewarmCount")]
        [Min(0)] public int PrewarmInstanceCount;

        /// <summary>对象池最多保留的视觉实例数量。</summary>
        [FormerlySerializedAs("MaximumRetained")]
        [Min(1)] public int MaximumRetainedInstanceCount = 256;
    }
}
