using Unity.Entities;

namespace ShootingSystem.Bullets.ECS.Components
{
    public struct ProjectileData : IComponentData
    {
        public float Damage;
        public float Speed;
        public float Radius;
        public Entity Owner;
        public int ProfileId;
    }
}
