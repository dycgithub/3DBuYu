using Unity.Entities;
using UnityEngine;
using Interfaces;

namespace ShootingSystem.Bullets.ECS.Systems
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(ProjectileDamageSystem))]
    public partial class HitscanSystem : SystemBase
    {
        private Collider[] _hitBuffer = new Collider[32];

        protected override void OnUpdate()
        {
            var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

            foreach (var (req, profile, entity) in SystemAPI.Query<Components.HitscanRequest, Components.HitscanProfileRef>().WithEntityAccess())
            {
                var pos = SystemAPI.GetComponent<Unity.Transforms.LocalTransform>(entity).Position;
                var dir = SystemAPI.GetComponent<Components.StraightTrajectory>(entity).Direction;

                var mask = 1 << LayerMask.NameToLayer("Enemy");
                if (Physics.Raycast(pos, dir, out RaycastHit hitInfo, profile.MaxDistance, mask, QueryTriggerInteraction.Collide))
                {
                    ecb.AddComponent(entity, new Components.HitEventData
                    {
                        TargetInstanceId = hitInfo.collider.gameObject.GetInstanceID(),
                        HitPoint = hitInfo.point,
                        BaseDamage = profile.Damage,
                        ProfileId = profile.ProfileId
                    });
                    ecb.AddComponent(entity, new Components.BulletDead());
                }
                else
                {
                    ecb.DestroyEntity(entity);
                }
            }

            ecb.Playback(EntityManager);
            ecb.Dispose();
        }
    }
}
