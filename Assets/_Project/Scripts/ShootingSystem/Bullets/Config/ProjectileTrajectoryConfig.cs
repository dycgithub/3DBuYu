using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ShootingSystem.Bullets.Config
{
    public class ProjectileTrajectoryConfig : TrajectoryConfig
    {
        public override void Initialize(EntityManager em, Entity entity, in SpawnRequest request)
        {
            em.SetComponentData(entity, LocalTransform.FromPositionRotation(request.Origin, quaternion.LookRotationSafe(request.Direction, math.up())));
            em.SetComponentData(entity, new ECS.Components.StraightTrajectory { Direction = request.Direction });
        }
    }
}
