using Unity.Entities;
using Unity.Mathematics;

namespace ShootingSystem.Bullets.ECS.Components
{
    public struct StraightTrajectory : IComponentData
    {
        public float3 Direction;
    }
}
