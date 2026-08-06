using Unity.Mathematics;

namespace ShootingSystem.Bullets.Effects
{
    public struct BulletHitEvent
    {
        public int TargetInstanceId;
        public float3 HitPoint;
        public float Damage;
        public int ProfileId;
    }

    public struct BulletExpiredEvent
    {
        public int EntityIndex;
        public int ProfileId;
    }

    public struct BulletSpawnedEvent
    {
        public int ProfileId;
        public float3 Origin;
        public float3 Direction;
    }
}
