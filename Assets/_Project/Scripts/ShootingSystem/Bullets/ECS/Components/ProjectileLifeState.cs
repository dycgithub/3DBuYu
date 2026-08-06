using Unity.Entities;

namespace ShootingSystem.Bullets.ECS.Components
{
    public struct ProjectileLifeState : IComponentData
    {
        public float Traveled;
        public float MaxDist;
        public byte IsDead;
    }
}
