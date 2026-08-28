using UnityEngine;

namespace CombatSystem
{
    public sealed class BuffRuntime
    {
        public BuffDefinition Definition { get; set; }
        public float TimeRemaining { get; set; }
        public int SourceId { get; set; }
        public int Stacks { get; set; } = 1;

        public bool IsExpired => Definition != null && Definition.Duration > 0f && TimeRemaining <= 0f;

        public void Tick(float deltaTime)
        {
            if (Definition != null && Definition.Duration > 0f)
                TimeRemaining -= Mathf.Max(0f, deltaTime);
        }
    }
}
