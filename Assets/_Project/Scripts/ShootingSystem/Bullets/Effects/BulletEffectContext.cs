using Unity.Mathematics;

namespace ShootingSystem.Bullets.Effects
{
    public struct BulletEffectContext
    {
        public int TargetInstanceId;
        public float3 HitPoint;
        public float Damage;
        public BulletProfile Profile;
    }
}
