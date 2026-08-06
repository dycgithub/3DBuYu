using Unity.Entities;
using UnityEngine;
using Interfaces;

namespace ShootingSystem.Bullets.ECS.Systems
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProjectileCollisionSystem))]
    [UpdateBefore(typeof(BulletEventFlushSystem))]
    public partial class ProjectileDamageSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (hit, life, entity) in SystemAPI.Query<RefRW<Components.HitEventData>, RefRW<Components.ProjectileLifeState>>().WithEntityAccess())
            {
                if (hit.ValueRO.Processed != 0) continue;

                var targetObj = Resources.EntityIdToObject(hit.ValueRO.TargetInstanceId) as GameObject;
                if (targetObj != null)
                {
                    var damageable = targetObj.GetComponentInParent<IDamageable>();
                    if (damageable != null && damageable.IsAlive)
                    {
                        damageable.TakeDamage(hit.ValueRO.BaseDamage);
                    }
                }

                hit.ValueRW.Processed = 1;
                life.ValueRW.IsDead = 1;
                ecb.AddComponent(entity, new Components.BulletDead());
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
