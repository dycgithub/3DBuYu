using UnityEngine;

namespace CombatSystem
{
    /// <summary>保存技能冷却等会随时间变化的运行时状态。</summary>
    public sealed class SkillRuntime
    {
        public SkillDefinition Definition { get; }
        public float CooldownRemaining { get; private set; }

        public bool IsReady => CooldownRemaining <= 0f;

        public SkillRuntime(SkillDefinition definition)
        {
            Definition = definition;
        }

        public void Tick(float deltaTime)
        {
            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Mathf.Max(0f, deltaTime));
        }

        public void StartCooldown()
        {
            CooldownRemaining = Definition != null ? Definition.Cooldown : 0f;
        }
    }
}
