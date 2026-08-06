using UnityEngine;
using Unity.Entities;
using VContainer;

namespace ShootingSystem.Bullets.ECS.Systems
{
    public class ShootingSystemBootstrap : MonoBehaviour
    {
        [Inject] private ProjectileVisualPool _visualPool;
        [Inject] private Effects.IBulletEventBus _eventBus;
        [Inject] private Effects.BulletEffectOrchestrator _effectOrchestrator;

        public ProjectileCollisionSystem CollisionSystem { get; private set; }
        public ProjectileDamageSystem DamageSystem { get; private set; }
        public ProjectileVisualSyncSystem VisualSyncSystem { get; private set; }
        public ProjectileCleanupSystem CleanupSystem { get; private set; }
        public BulletEventFlushSystem EventFlushSystem { get; private set; }
        public HitscanSystem HitscanSystem { get; private set; }

        private SystemHandle _movementHandle;
        private SystemHandle _lifetimeHandle;

        private void Start()
        {
            _effectOrchestrator?.SetCoroutineHost(this);

            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null)
            {
                Debug.LogWarning("[ShootingSystemBootstrap] No default ECS world");
                return;
            }

            var simGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
            if (simGroup == null)
            {
                Debug.LogWarning("[ShootingSystemBootstrap] SimulationSystemGroup not found");
                return;
            }

            // ISystem structs
            _movementHandle = world.GetOrCreateSystem<ProjectileMovementSystem>();
            _lifetimeHandle = world.GetOrCreateSystem<ProjectileLifetimeSystem>();
            simGroup.AddSystemToUpdateList(_movementHandle);
            simGroup.AddSystemToUpdateList(_lifetimeHandle);

            // SystemBase managed systems
            HitscanSystem = world.CreateSystemManaged<HitscanSystem>();
            CollisionSystem = world.CreateSystemManaged<ProjectileCollisionSystem>();
            DamageSystem = world.CreateSystemManaged<ProjectileDamageSystem>();
            VisualSyncSystem = world.CreateSystemManaged<ProjectileVisualSyncSystem>();
            EventFlushSystem = world.CreateSystemManaged<BulletEventFlushSystem>();
            CleanupSystem = world.CreateSystemManaged<ProjectileCleanupSystem>();

            VisualSyncSystem.SetServices(_visualPool);
            CleanupSystem.SetServices(_visualPool);
            EventFlushSystem.SetServices(_eventBus);

            simGroup.AddSystemToUpdateList(HitscanSystem);
            simGroup.AddSystemToUpdateList(CollisionSystem);
            simGroup.AddSystemToUpdateList(DamageSystem);
            simGroup.AddSystemToUpdateList(VisualSyncSystem);
            simGroup.AddSystemToUpdateList(EventFlushSystem);
            simGroup.AddSystemToUpdateList(CleanupSystem);

            simGroup.SortSystems();

            Debug.Log("[ShootingSystemBootstrap] Systems registered");
        }

        private void OnDestroy()
        {
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null) return;

            var simGroup = world.GetExistingSystemManaged<SimulationSystemGroup>();
            if (simGroup == null) return;

            if (HitscanSystem != null) simGroup.RemoveSystemFromUpdateList(HitscanSystem);
            if (CollisionSystem != null) simGroup.RemoveSystemFromUpdateList(CollisionSystem);
            if (DamageSystem != null) simGroup.RemoveSystemFromUpdateList(DamageSystem);
            if (VisualSyncSystem != null) simGroup.RemoveSystemFromUpdateList(VisualSyncSystem);
            if (EventFlushSystem != null) simGroup.RemoveSystemFromUpdateList(EventFlushSystem);
            if (CleanupSystem != null) simGroup.RemoveSystemFromUpdateList(CleanupSystem);

            if (HitscanSystem != null) world.DestroySystemManaged(HitscanSystem);
            if (CollisionSystem != null) world.DestroySystemManaged(CollisionSystem);
            if (DamageSystem != null) world.DestroySystemManaged(DamageSystem);
            if (VisualSyncSystem != null) world.DestroySystemManaged(VisualSyncSystem);
            if (EventFlushSystem != null) world.DestroySystemManaged(EventFlushSystem);
            if (CleanupSystem != null) world.DestroySystemManaged(CleanupSystem);

            if (_movementHandle != SystemHandle.Null)
            {
                simGroup.RemoveSystemFromUpdateList(_movementHandle);
                world.DestroySystem(_movementHandle);
                _movementHandle = SystemHandle.Null;
            }
            if (_lifetimeHandle != SystemHandle.Null)
            {
                simGroup.RemoveSystemFromUpdateList(_lifetimeHandle);
                world.DestroySystem(_lifetimeHandle);
                _lifetimeHandle = SystemHandle.Null;
            }

            HitscanSystem = null;
            CollisionSystem = null;
            DamageSystem = null;
            VisualSyncSystem = null;
            EventFlushSystem = null;
            CleanupSystem = null;

            Debug.Log("[ShootingSystemBootstrap] Systems removed");
        }
    }
}
