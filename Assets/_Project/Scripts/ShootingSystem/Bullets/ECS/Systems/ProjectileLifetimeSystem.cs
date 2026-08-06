using ShootingSystem.Bullets.ECS.Components;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

namespace ShootingSystem.Bullets.ECS.Systems
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProjectileMovementSystem))]
    [UpdateBefore(typeof(ProjectileCollisionSystem))]
    [BurstCompile]
    public partial struct ProjectileLifetimeSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float dt = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            state.Dependency = new LifeJob { Dt = dt, Ecb = ecb.AsParallelWriter() }
                .ScheduleParallel(state.Dependency);
            state.Dependency.Complete();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        private partial struct LifeJob : IJobEntity
        {
            public float Dt;
            public EntityCommandBuffer.ParallelWriter Ecb;

            private void Execute([ChunkIndexInQuery] int sortKey, Entity entity, ref ProjectileLifeState life, in ProjectileData data)
            {
                if (life.IsDead != 0) return;
                life.Traveled += data.Speed * Dt;
                if (life.Traveled >= life.MaxDist)
                {
                    life.IsDead = 1;
                    // 距离超限的子弹补一个 BulletDead 标签，
                    // 让 BulletEventFlushSystem.EmitExpired 和 ProjectileCleanupSystem 接管回收。
                    Ecb.AddComponent<BulletDead>(sortKey, entity);
                }
            }
        }
    }
}
