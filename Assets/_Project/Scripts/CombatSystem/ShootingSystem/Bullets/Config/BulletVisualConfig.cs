using UnityEngine;

namespace CombatSystem
{
    /// <summary>子弹视觉预制体和命中表现的静态配置。</summary>
    [CreateAssetMenu(menuName = "ShootingSystem/Bullet Visual Config")]
    public class BulletVisualConfig : ScriptableObject
    {
        public GameObject Prefab;
        public Color Color = Color.yellow;
        public GameObject HitVfxPrefab;
        public GameObject ExpiredVfxPrefab;

        [Header("对象池")]
        [Min(0)] public int PrewarmCount;
        [Min(1)] public int MaximumRetained = 256;
    }
}
