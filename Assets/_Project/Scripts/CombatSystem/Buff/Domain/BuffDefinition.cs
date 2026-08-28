using UnityEngine;

namespace CombatSystem
{
    [CreateAssetMenu(menuName = "Combat/Buff Definition")]
    public sealed class BuffDefinition : ScriptableObject
    {
        public BuffType Type;
        public float Duration;
        public float Value;
        public BuffStackPolicy StackPolicy = BuffStackPolicy.RefreshDuration;
        [Min(1)] public int MaxStacks = 1;
        public GameObject VisualEffect;
    }
}
