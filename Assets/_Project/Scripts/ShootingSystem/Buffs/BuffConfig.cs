using UnityEngine;

namespace ShootingSystem.Buffs
{
    [CreateAssetMenu(menuName = "ShootingSystem/Buff Config")]
    public class BuffConfig : ScriptableObject
    {
        public BuffType Type;
        public float Duration;
        public float Value;
        public GameObject VisualEffect;
    }
}
