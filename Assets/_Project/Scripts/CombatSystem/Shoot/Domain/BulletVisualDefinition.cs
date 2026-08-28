using UnityEngine;

namespace CombatSystem
{
    [CreateAssetMenu(menuName = "Combat/Shoot Bullet Visual")]
    public sealed class BulletVisualDefinition : ScriptableObject
    {
        public GameObject Prefab;
        public Color Color = Color.yellow;
        public GameObject HitVfxPrefab;
        public GameObject ExpiredVfxPrefab;
        [Min(0)] public int PrewarmCount;
        [Min(1)] public int MaximumRetained = 256;
    }
}
