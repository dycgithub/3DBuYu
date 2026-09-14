using Interfaces;
using NUnit.Framework;
using UnityEngine;

namespace CombatSystem.Tests
{
    /// <summary>
    /// 验证投射物距离边界和轨迹类型组合的运行时契约。
    /// </summary>
    public sealed class ProjectileSimulationContractTests
    {
        [Test]
        public void ProjectileMovement_IsClampedToRemainingMaximumDistance()
        {
            ProjectileProfileSO profile = ProjectileProfileSO.CreateRuntimeProfile("distance-test");
            var query = new RecordingCollisionQuery();
            var simulation = new ProjectileSimulationService(
                new ProjectileViewPool(null),
                new ProjectileRuntimePool(),
                query,
                new NoOpDamageProcessor(),
                new NoOpEffectDispatcher());
            ProjectileData projectile = new ProjectileData
            {
                ProjectileProfile = profile,
                OriginPosition = Vector3.zero,
                LaunchDirection = Vector3.forward,
                DamageAmount = 1f,
                SpeedUnitsPerSecond = 100f,
                MaximumTravelDistance = 1f,
                CollisionRadius = 0.1f,
                DamageType = DamageType.Physical
            };

            try
            {
                Assert.That(simulation.TrySpawnProjectile(in projectile), Is.True);
                simulation.SimulateStep(1f);

                Assert.That(query.LastSweepDistance, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(simulation.ActiveProjectileCount, Is.Zero);
            }
            finally
            {
                simulation.Dispose();
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ZeroMaximumTravelDistance_ExpiresWithoutSweep()
        {
            ProjectileProfileSO profile = ProjectileProfileSO.CreateRuntimeProfile("zero-distance-test");
            var query = new RecordingCollisionQuery();
            var simulation = new ProjectileSimulationService(
                new ProjectileViewPool(null),
                new ProjectileRuntimePool(),
                query,
                new NoOpDamageProcessor(),
                new NoOpEffectDispatcher());
            ProjectileData projectile = new ProjectileData
            {
                ProjectileProfile = profile,
                OriginPosition = Vector3.zero,
                LaunchDirection = Vector3.forward,
                DamageAmount = 1f,
                SpeedUnitsPerSecond = 15f,
                MaximumTravelDistance = 0f,
                CollisionRadius = 0.1f,
                DamageType = DamageType.Physical
            };

            try
            {
                Assert.That(simulation.TrySpawnProjectile(in projectile), Is.True);
                simulation.SimulateStep(0f);

                Assert.That(query.SweepCallCount, Is.Zero);
                Assert.That(simulation.ActiveProjectileCount, Is.Zero);
            }
            finally
            {
                simulation.Dispose();
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void HitscanWithPenetration_IsRejectedDuringAttackBuild()
        {
            ProjectileProfileSO profile = ProjectileProfileSO.CreateRuntimeProfile("hitscan-test");
            HitscanTrajectoryDefinitionSO trajectory =
                ScriptableObject.CreateInstance<HitscanTrajectoryDefinitionSO>();
            profile.TrajectoryDefinition = trajectory;
            var input = new TransmitterAttackInput(
                1,
                0,
                profile,
                Vector3.zero,
                Vector3.forward,
                10f,
                50f,
                2f,
                1,
                1,
                0f,
                1f);
            var buildService = new TransmitterAttackBuildService(null);
            TransmitterAttackBuild build = buildService.BuildAttackParameters(in input);
            var builder = new AttackDataBuilder();

            try
            {
                Assert.That(builder.TryBuildAttackData(in input, in build, out _), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(trajectory);
                Object.DestroyImmediate(profile);
            }
        }

        private sealed class RecordingCollisionQuery : IProjectileCollisionQuery
        {
            public float LastSweepDistance { get; private set; }
            public int SweepCallCount { get; private set; }

            public bool TryFindSweepCollision(
                ProjectileRuntime projectile,
                float travelDistance,
                out RaycastHit hit,
                out IDamageable target)
            {
                LastSweepDistance = travelDistance;
                SweepCallCount++;
                hit = default;
                target = null;
                return false;
            }

            public bool TryFindRaycastCollision(
                ProjectileData projectileData,
                out RaycastHit hit,
                out IDamageable target)
            {
                hit = default;
                target = null;
                return false;
            }
        }

        private sealed class NoOpDamageProcessor : IDamageRequestProcessor
        {
            public bool TryApplyDamage(
                IDamageable target,
                in DamageRequest request,
                out DamageResult result)
            {
                result = default;
                return false;
            }
        }

        private sealed class NoOpEffectDispatcher : IProjectileEffectDispatcher
        {
            public void DispatchHitEffects(
                ProjectileProfileSO projectileProfile,
                DamageRequest request,
                DamageResult result,
                IDamageable target)
            {
            }

            public void DispatchBeforeHitEffects(
                ProjectileProfileSO projectileProfile,
                DamageRequest request,
                IDamageable target)
            {
            }

            public void DispatchExpiredEffects(
                ProjectileProfileSO projectileProfile,
                Vector3 position)
            {
            }
        }
    }
}
