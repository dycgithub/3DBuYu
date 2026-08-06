using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using ShootingSystem.Bullets.ECS.Components;
using ShootingSystem.Bullets.Config;
using UnityEngine;

namespace ShootingSystem
{
    public class EcsBulletSpawner : IBulletSpawner
    {
        private ProjectileVisualPool _visualPool;

        public EcsBulletSpawner(ProjectileVisualPool visualPool)
        {
            _visualPool = visualPool;
        }

        public void Spawn(SpawnRequest request)
        {
            Debug.Log("生成子弹");
            if (request.Profile == null) return;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;
            var em = world.EntityManager;

            if (request.Profile.Trajectory is HitscanTrajectoryConfig)
            {
                SpawnHitscan(em, request);
            }
            else if (request.Profile.Trajectory is ProjectileTrajectoryConfig)
            {
                SpawnProjectile(em, request);
            }
        }

        private void SpawnHitscan(EntityManager em, SpawnRequest request)
        {
            var entity = em.CreateEntity();
            em.AddComponentData(entity, LocalTransform.FromPositionRotation(request.Origin, quaternion.LookRotationSafe(request.Direction, math.up())));
            em.AddComponentData(entity, new StraightTrajectory { Direction = request.Direction });
            em.AddComponentData(entity, new HitscanRequest());
            em.AddComponentData(entity, new HitscanProfileRef
            {
                Damage = request.DamageOverride > 0f ? request.DamageOverride : request.Profile.Damage,
                Radius = request.Profile.Radius,
                MaxDistance = request.Profile.MaxDistance,
                ProfileId = request.Profile.GetInstanceID()
            });
            em.AddComponentData(entity, new ProjectileLifeState
            {
                Traveled = 0f,
                MaxDist = request.Profile.MaxDistance
            });
        }

        private void SpawnProjectile(EntityManager em, SpawnRequest request)
        {
            Debug.Log("生成投射物");
            int slot = _visualPool.Allocate(request.Profile, request.Origin, quaternion.LookRotationSafe(request.Direction, math.up()));

            var entity = em.CreateEntity();
            em.AddComponentData(entity, LocalTransform.FromPositionRotation(request.Origin, quaternion.LookRotationSafe(request.Direction, math.up())));
            em.AddComponentData(entity, new StraightTrajectory { Direction = request.Direction });
            em.AddComponentData(entity, new ProjectileData
            {
                Damage = request.DamageOverride > 0f ? request.DamageOverride : request.Profile.Damage,
                Speed = request.Profile.Speed,
                Radius = request.Profile.Radius,
                Owner = request.Owner,
                ProfileId = request.Profile.GetInstanceID()
            });
            em.AddComponentData(entity, new ProjectileLifeState
            {
                Traveled = 0f,
                MaxDist = request.Profile.MaxDistance
            });
            em.AddComponentData(entity, new ProjectileVisualRef { PoolSlot = slot });
        }
    }
}
