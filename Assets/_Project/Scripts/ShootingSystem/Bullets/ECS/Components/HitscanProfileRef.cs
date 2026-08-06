using Unity.Entities;

namespace ShootingSystem.Bullets.ECS.Components
{
    public struct HitscanProfileRef : IComponentData
    {
        public float Damage;
        public float Radius;
        public float MaxDistance;
        public int ProfileId;
    }
}
