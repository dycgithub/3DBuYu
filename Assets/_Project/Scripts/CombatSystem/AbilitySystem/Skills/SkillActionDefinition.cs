using UnityEngine;

namespace CombatSystem
{
    /// <summary>技能中的一个可组合动作。</summary>
    public abstract class SkillActionDefinition : ScriptableObject
    {
        public abstract bool Execute(in SkillInfo info, SkillExecutionContext context);
    }

    /// <summary>技能动作执行时可使用的运行时服务。</summary>
    public sealed class SkillExecutionContext
    {
        public IProjectileSpawner ProjectileSpawner { get; }

        public SkillExecutionContext(IProjectileSpawner projectileSpawner)
        {
            ProjectileSpawner = projectileSpawner;
        }
    }
}
