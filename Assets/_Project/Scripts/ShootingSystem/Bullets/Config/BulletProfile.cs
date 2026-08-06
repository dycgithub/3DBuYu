using System.Collections.Generic;
using UnityEngine;

namespace ShootingSystem
{
    [CreateAssetMenu(menuName = "ShootingSystem/Bullet Profile")]
    public class BulletProfile : ScriptableObject
    {
        public float Damage;
        public float Speed;
        public float MaxDistance;
        public float Radius;
        public Bullets.Config.TrajectoryConfig Trajectory;
        public BulletVisualConfig Visual;
        public List<Bullets.Effects.BulletEffectConfig> OnHitEffects;
        public List<Bullets.Effects.BulletEffectConfig> OnExpiredEffects;
        public List<Bullets.Effects.BulletEffectConfig> OnTriggerEffects;
    }
}
