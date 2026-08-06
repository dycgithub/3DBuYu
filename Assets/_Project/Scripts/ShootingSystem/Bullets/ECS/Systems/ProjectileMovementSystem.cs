using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace ShootingSystem.Bullets.ECS.Systems
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(ProjectileCollisionSystem))]
    [BurstCompile]
    public partial struct ProjectileMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            new MoveJob { Dt = dt }.ScheduleParallel();
        }

        [BurstCompile]
        private partial struct MoveJob : IJobEntity
        {
            public float Dt;
            private void Execute(ref LocalTransform transform, in Components.StraightTrajectory traj, in Components.ProjectileData data, in Components.ProjectileLifeState life)
            {
                if (life.IsDead != 0) return;
                transform.Position += traj.Direction * data.Speed * Dt;
            }
        }
    }
}
