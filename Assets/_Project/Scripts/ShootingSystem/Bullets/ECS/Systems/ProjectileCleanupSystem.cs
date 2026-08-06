using Unity.Entities;

namespace ShootingSystem.Bullets.ECS.Systems
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(BulletEventFlushSystem))]
    public partial class ProjectileCleanupSystem : SystemBase
    {
        private ProjectileVisualPool _pool;

        public void SetServices(ProjectileVisualPool pool)
        {
            _pool = pool;
        }

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (visual, entity) in SystemAPI.Query<Components.ProjectileVisualRef>().WithAll<Components.BulletDead>().WithEntityAccess())
            {
                _pool?.Release(visual.PoolSlot);
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
