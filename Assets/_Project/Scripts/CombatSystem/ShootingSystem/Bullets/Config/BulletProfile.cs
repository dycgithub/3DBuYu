using System.Collections.Generic;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>子弹的静态配置，运行时状态由 ProjectileInfo 和 ProjectileRuntime 保存。</summary>
    [CreateAssetMenu(menuName = "ShootingSystem/Bullet Profile")]
    public class BulletProfile : ScriptableObject
    {
        public float Damage;
        [Min(0f)] public float EnergyCost = 1f;
        public float Speed;
        public float MaxDistance;
        public float Radius;
        public DamageType DamageType = DamageType.Physical;
        public TrajectoryConfig Trajectory;
        public BulletVisualConfig Visual;
        public List<BulletEffectConfig> OnHitEffects;
        public List<BulletEffectConfig> OnExpiredEffects;
        public List<BulletEffectConfig> OnTriggerEffects;
    }
}
