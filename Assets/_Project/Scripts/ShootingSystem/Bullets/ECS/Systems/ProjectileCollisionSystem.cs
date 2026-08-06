using Unity.Entities;
using UnityEngine;
using Interfaces;

namespace ShootingSystem.Bullets.ECS.Systems
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProjectileLifetimeSystem))]
    [UpdateBefore(typeof(ProjectileDamageSystem))]
    public partial class ProjectileCollisionSystem : SystemBase
    {
        private Collider[] _hitBuffer = new Collider[32];

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (data, life, traj, entity) in SystemAPI.Query<Components.ProjectileData, RefRW<Components.ProjectileLifeState>, Components.StraightTrajectory>().WithEntityAccess())
            {
                if (life.ValueRO.IsDead != 0) continue;
                var pos = SystemAPI.GetComponent<Unity.Transforms.LocalTransform>(entity).Position;

                var mask = 1 << LayerMask.NameToLayer("Enemy");
                int hits = Physics.OverlapSphereNonAlloc(pos, data.Radius, _hitBuffer, mask, QueryTriggerInteraction.Collide);
                for (int i = 0; i < hits; i++)
                {
                    var col = _hitBuffer[i];
                    if (col == null) continue;
                    var d = col.GetComponentInParent<IDamageable>();
                    if (d == null || !d.IsAlive) continue;

                    ecb.AddComponent(entity, new Components.HitEventData
                    {
                        TargetInstanceId = col.gameObject.GetInstanceID(),
                        HitPoint = pos,
                        BaseDamage = data.Damage,
                        ProfileId = data.ProfileId
                    });
                    break;
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
