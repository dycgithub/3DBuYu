using Services;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>描述投射物命中、命中前或过期时执行的效果策略。</summary>
    public abstract class ProjectileEffectDefinitionSO : ScriptableObject
    {
        /// <summary>在投射物效果上下文中执行当前效果。</summary>
        public abstract void ExecuteEffect(ProjectileEffectContext context, IEffectService effectService);
    }
}
