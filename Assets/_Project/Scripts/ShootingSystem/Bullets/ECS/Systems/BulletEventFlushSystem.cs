using Unity.Entities;
using UnityEngine;

namespace ShootingSystem.Bullets.ECS.Systems
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProjectileDamageSystem))]
    [UpdateBefore(typeof(ProjectileCleanupSystem))]
    public partial class BulletEventFlushSystem : SystemBase
    {
        private Effects.IBulletEventBus _eventBus;

        public void SetServices(Effects.IBulletEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        protected override void OnUpdate()
        {
            if (_eventBus == null) return;

            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (hit, entity) in SystemAPI.Query<Components.HitEventData>().WithAll<Components.BulletDead>().WithEntityAccess())
            {
                if (hit.Processed != 0)
                {
                    _eventBus.EmitHit(hit.TargetInstanceId, hit.HitPoint, hit.BaseDamage, hit.ProfileId);
                }
            }

            foreach (var (life, data, entity) in SystemAPI.Query<Components.ProjectileLifeState, Components.ProjectileData>().WithAll<Components.BulletDead>().WithNone<Components.HitEventData>().WithEntityAccess())
            {
                _eventBus.EmitExpired(entity.Index, data.ProfileId);
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
