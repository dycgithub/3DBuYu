using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace ShootingSystem.Bullets.ECS.Systems
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProjectileMovementSystem))]
    [UpdateBefore(typeof(ProjectileCleanupSystem))]
    public partial class ProjectileVisualSyncSystem : SystemBase
    {
        private ProjectileVisualPool _pool;

        public void SetServices(ProjectileVisualPool pool)
        {
            _pool = pool;
        }

        protected override void OnUpdate()
        {
            if (_pool == null) return;

            foreach (var (transform, visual, life) in SystemAPI.Query<LocalTransform, Components.ProjectileVisualRef, Components.ProjectileLifeState>())
            {
                if (life.IsDead != 0) continue;
                _pool.UpdateTransform(visual.PoolSlot, transform.Position, transform.Rotation);
            }
        }
    }
}
