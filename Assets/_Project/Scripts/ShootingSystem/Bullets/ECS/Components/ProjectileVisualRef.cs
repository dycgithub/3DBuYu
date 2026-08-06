using Unity.Entities;

namespace ShootingSystem.Bullets.ECS.Components
{
    public struct ProjectileVisualRef : IComponentData
    {
        public int PoolSlot;
    }
}
