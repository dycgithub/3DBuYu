using Services;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>子弹表现或附加行为的静态效果配置。</summary>
    public abstract class BulletEffectConfig : ScriptableObject
    {
        public abstract void Execute(BulletEffectContext context, IPooledEffectService effectService);
    }
}
