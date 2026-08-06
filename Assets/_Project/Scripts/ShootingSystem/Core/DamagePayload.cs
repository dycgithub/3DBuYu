using Unity.Entities;
using Unity.Mathematics;

namespace ShootingSystem
{
    public struct DamagePayload
    {
        public float BaseDamage;
        public float FinalDamage;
        public Entity Source;
        public float3 HitPoint;
        public float3 HitNormal;
    }
}
