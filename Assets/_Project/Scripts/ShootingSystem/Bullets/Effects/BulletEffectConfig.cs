using UnityEngine;

namespace ShootingSystem.Bullets.Effects
{
    public abstract class BulletEffectConfig : ScriptableObject
    {
        public abstract void Execute(BulletEffectContext context);
    }
}
