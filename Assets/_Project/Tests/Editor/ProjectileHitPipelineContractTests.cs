using System.Collections.Generic;
using CombatSystem;
using Interfaces;
using NUnit.Framework;
using UnityEngine;

namespace CombatSystem.Tests
{
    /// <summary>
    /// 验证投射物生成后的数据快照，以及普通投射物和 Hitscan 的统一命中契约。
    /// </summary>
    public sealed class ProjectileHitPipelineContractTests
    {
        [Test]
        public void ProjectileKeepsSpawnSnapshot()
        {
            ProjectileProfileSO profile = ProjectileProfileSO.CreateRuntimeProfile("snapshot-test");
            profile.SpeedUnitsPerSecond = 10f;
            profile.MaximumTravelDistance = 20f;
            profile.CollisionRadius = 0.25f;
            profile.DamageType = DamageType.Fire;
            var query = new RecordingCollisionQuery(null, false);
            var simulation = new ProjectileSimulationService(
                new ProjectileViewPool(null),
                new ProjectileRuntimePool(),
                query,
                null,
                null);
            ProjectileData projectile = ProjectileDataFactory.CreateFromParameters(
                attackInstanceId: 7,
                sourceInstanceId: 11,
                projectileProfile: profile,
                originPosition: Vector3.zero,
                launchDirection: Vector3.forward,
                damageAmount: 12f,
                wasCritical: true);

            try
            {
                Assert.That(simulation.TrySpawnProjectile(in projectile), Is.True);

                profile.SpeedUnitsPerSecond = 100f;
                profile.MaximumTravelDistance = 1000f;
                profile.CollisionRadius = 5f;
                profile.DamageType = DamageType.Ice;
                simulation.SimulateStep(0.1f);

                Assert.That(query.LastSweepDistance, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(query.LastProjectileData.SpeedUnitsPerSecond, Is.EqualTo(10f));
                Assert.That(query.LastProjectileData.MaximumTravelDistance, Is.EqualTo(20f));
                Assert.That(query.LastProjectileData.CollisionRadius, Is.EqualTo(0.25f));
                Assert.That(query.LastProjectileData.DamageType, Is.EqualTo(DamageType.Fire));
            }
            finally
            {
                simulation.Dispose();
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void HitscanAndProjectileUseSameHitPipeline()
        {
            RecordingHitPipeline projectilePipeline = ExecuteHit(false);
            RecordingHitPipeline hitscanPipeline = ExecuteHit(true);

            Assert.That(projectilePipeline.DamageCallCount, Is.EqualTo(1));
            Assert.That(hitscanPipeline.DamageCallCount, Is.EqualTo(1));
            CollectionAssert.AreEqual(
                new[] { "before", "damage", "hit" },
                projectilePipeline.Events);
            CollectionAssert.AreEqual(
                new[] { "before", "damage", "hit" },
                hitscanPipeline.Events);

            AssertRequestMatches(projectilePipeline.DamageRequest, hitscanPipeline.DamageRequest);
            AssertRequestMatches(
                projectilePipeline.DamageRequest,
                projectilePipeline.BeforeHitRequest);
            AssertRequestMatches(
                projectilePipeline.DamageRequest,
                projectilePipeline.HitRequest);
            AssertRequestMatches(
                hitscanPipeline.DamageRequest,
                hitscanPipeline.BeforeHitRequest);
            AssertRequestMatches(
                hitscanPipeline.DamageRequest,
                hitscanPipeline.HitRequest);
        }

        private static RecordingHitPipeline ExecuteHit(bool hitscan)
        {
            ProjectileProfileSO profile = ProjectileProfileSO.CreateRuntimeProfile(
                hitscan ? "hitscan-hit-test" : "projectile-hit-test");
            HitscanTrajectoryDefinitionSO trajectory = null;
            if (hitscan)
            {
                trajectory = ScriptableObject.CreateInstance<HitscanTrajectoryDefinitionSO>();
                profile.TrajectoryDefinition = trajectory;
            }

            var pipeline = new RecordingHitPipeline();
            var target = new RecordingTarget();
            var simulation = new ProjectileSimulationService(
                new ProjectileViewPool(null),
                new ProjectileRuntimePool(),
                new RecordingCollisionQuery(target, true),
                new RecordingDamageProcessor(pipeline),
                new RecordingEffectDispatcher(pipeline));
            ProjectileData projectile = new ProjectileData
            {
                AttackInstanceId = 17,
                SourceInstanceId = 23,
                ProjectileProfile = profile,
                OriginPosition = Vector3.zero,
                LaunchDirection = Vector3.forward,
                DamageAmount = 9f,
                SpeedUnitsPerSecond = 10f,
                MaximumTravelDistance = 20f,
                CollisionRadius = 0.1f,
                PenetrationCount = 0,
                DamageType = DamageType.Lightning,
                WasCritical = true
            };

            try
            {
                Assert.That(simulation.TrySpawnProjectile(in projectile), Is.True);
                if (!hitscan)
                {
                    simulation.SimulateStep(0.1f);
                }

                return pipeline;
            }
            finally
            {
                simulation.Dispose();
                Object.DestroyImmediate(trajectory);
                Object.DestroyImmediate(profile);
            }
        }

        private static void AssertRequestMatches(
            DamageRequest expected,
            DamageRequest actual)
        {
            Assert.That(actual.AttackInstanceId, Is.EqualTo(expected.AttackInstanceId));
            Assert.That(actual.SourceInstanceId, Is.EqualTo(expected.SourceInstanceId));
            Assert.That(actual.DamageAmount, Is.EqualTo(expected.DamageAmount));
            Assert.That(actual.DamageType, Is.EqualTo(expected.DamageType));
            Assert.That(actual.HitPosition, Is.EqualTo(expected.HitPosition));
            Assert.That(actual.HitNormal, Is.EqualTo(expected.HitNormal));
            Assert.That(actual.WasCritical, Is.EqualTo(expected.WasCritical));
        }

        private sealed class RecordingCollisionQuery : IProjectileCollisionQuery
        {
            private readonly IDamageable _target;
            private readonly bool _shouldHit;

            public float LastSweepDistance { get; private set; }
            public ProjectileData LastProjectileData { get; private set; }

            public RecordingCollisionQuery(IDamageable target, bool shouldHit)
            {
                _target = target;
                _shouldHit = shouldHit;
            }

            public bool TryFindSweepCollision(
                ProjectileRuntime projectile,
                float travelDistance,
                out RaycastHit hit,
                out IDamageable target)
            {
                LastSweepDistance = travelDistance;
                LastProjectileData = projectile.Data;
                hit = CreateHit();
                target = _target;
                return _shouldHit;
            }

            public bool TryFindRaycastCollision(
                ProjectileData projectileData,
                out RaycastHit hit,
                out IDamageable target)
            {
                LastProjectileData = projectileData;
                hit = CreateHit();
                target = _target;
                return _shouldHit;
            }

            private static RaycastHit CreateHit()
            {
                return new RaycastHit
                {
                    point = new Vector3(2f, 3f, 4f),
                    normal = Vector3.up
                };
            }
        }

        private sealed class RecordingDamageProcessor : IDamageRequestProcessor
        {
            private readonly RecordingHitPipeline _pipeline;

            public RecordingDamageProcessor(RecordingHitPipeline pipeline)
            {
                _pipeline = pipeline;
            }

            public bool TryApplyDamage(
                IDamageable target,
                in DamageRequest request,
                out DamageResult result)
            {
                _pipeline.Events.Add("damage");
                _pipeline.DamageCallCount++;
                _pipeline.DamageRequest = request;
                result = new DamageResult
                {
                    Outcome = DamageOutcome.Applied,
                    AppliedDamage = request.DamageAmount
                };
                return true;
            }
        }

        private sealed class RecordingEffectDispatcher : IProjectileEffectDispatcher
        {
            private readonly RecordingHitPipeline _pipeline;

            public RecordingEffectDispatcher(RecordingHitPipeline pipeline)
            {
                _pipeline = pipeline;
            }

            public void DispatchHitEffects(
                ProjectileProfileSO projectileProfile,
                DamageRequest request,
                DamageResult result,
                IDamageable target)
            {
                _pipeline.Events.Add("hit");
                _pipeline.HitRequest = request;
            }

            public void DispatchBeforeHitEffects(
                ProjectileProfileSO projectileProfile,
                DamageRequest request,
                IDamageable target)
            {
                _pipeline.Events.Add("before");
                _pipeline.BeforeHitRequest = request;
            }

            public void DispatchExpiredEffects(
                ProjectileProfileSO projectileProfile,
                Vector3 position)
            {
                _pipeline.Events.Add("expired");
            }
        }

        private sealed class RecordingHitPipeline
        {
            public readonly List<string> Events = new();
            public int DamageCallCount;
            public DamageRequest DamageRequest;
            public DamageRequest BeforeHitRequest;
            public DamageRequest HitRequest;
        }

        private sealed class RecordingTarget : IDamageable
        {
            public Vector3 Position => Vector3.zero;
            public bool IsAlive => true;
            public Transform Transform => null;
        }
    }
}
