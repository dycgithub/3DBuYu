using UnityEngine;

namespace ShootingSystem
{
    [CreateAssetMenu(menuName = "ShootingSystem/Bullet Visual Config")]
    public class BulletVisualConfig : ScriptableObject
    {
        public GameObject Prefab;
        public Color Color = Color.yellow;
        public GameObject HitVfxPrefab;
        public GameObject ExpiredVfxPrefab;
    }
}
