using System.Collections.Generic;
using UnityEngine;
using EffectSystem;

namespace CombatSystem
{
    [CreateAssetMenu(menuName = "Combat/Shoot Bullet Profile")]
    public sealed class BulletProfile : ScriptableObject
    {
        public float Damage = 10f;
        [Min(0f)] public float EnergyCost = 1f;
        public float Speed = 15f;
        public float MaxDistance = 50f;
        public float Radius = 0.1f;
        public DamageType DamageType = DamageType.Physical;
        public TrajectoryDefinition Trajectory;
        public BulletVisualDefinition Visual;
        public EffectId HitEffect = EffectId.BulletHit;
        public EffectId ExpiredEffect = EffectId.BulletExpired;
        public List<BulletEffectDefinition> OnHitEffects;
        public List<BulletEffectDefinition> OnExpiredEffects;
        public List<BulletEffectDefinition> OnTriggerEffects;

        private static BulletProfile _default;

        public static BulletProfile Default
        {
            get
            {
                if (_default == null)
                    _default = CreateRuntime("RuntimeShootProfile");
                return _default;
            }
        }

        public static BulletProfile CreateRuntime(string profileName)
        {
            BulletProfile profile = CreateInstance<BulletProfile>();
            profile.name = string.IsNullOrEmpty(profileName) ? "RuntimeShootProfile" : profileName;
            return profile;
        }
    }
}
