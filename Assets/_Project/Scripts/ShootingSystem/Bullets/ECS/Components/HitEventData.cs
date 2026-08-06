using Unity.Entities;
using Unity.Mathematics;

namespace ShootingSystem.Bullets.ECS.Components
{
    public struct HitEventData : IComponentData
    {
        public int TargetInstanceId;
        public float3 HitPoint;
        public float BaseDamage;
        public int ProfileId;
        public byte Processed;
    }
}
