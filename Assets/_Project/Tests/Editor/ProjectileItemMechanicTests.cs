using System.Collections.Generic;
using CombatSystem;
using Interfaces;
using NUnit.Framework;
using Services;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证发射器 003、007、010、011、014 的投射物机制和对象池边界。
    /// </summary>
    public sealed class ProjectileItemMechanicTests
    {
        private readonly List<Object> _temporaryObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int index = _temporaryObjects.Count - 1; index >= 0; index--)
            {
                if (_temporaryObjects[index] != null)
                {
                    Object.DestroyImmediate(_temporaryObjects[index]);
                }
            }

            _temporaryObjects.Clear();
        }

        [Test]
        public void HomingTrajectory_TurnsTowardAChangingTarget()
        {
            HomingTrajectoryDefinitionSO trajectory = Track(
                ScriptableObject.CreateInstance<HomingTrajectoryDefinitionSO>());
            TestDamageTarget target = CreateTarget("HomingTarget", new Vector3(10f, 0f, 10f));
            ProjectileRuntime projectile = new(new ProjectileData
            {
                LaunchDirection = Vector3.forward,
                SpeedUnitsPerSecond = 10f,
                MaximumTravelDistance = 20f
            }, null);
            projectile.TrackingTarget = target;
            projectile.SimulationDeltaTime = 0.1f;

            Vector3 firstDirection = trajectory.GetDirection(projectile, projectile.Direction);
            target.transform.position = new Vector3(-10f, 0f, 10f);
            projectile.Direction = firstDirection;
            Vector3 secondDirection = trajectory.GetDirection(projectile, projectile.Direction);

            Assert.That(firstDirection.x, Is.GreaterThan(0f));
            Assert.That(secondDirection.x, Is.LessThan(0f));
            Assert.That(secondDirection, Is.Not.EqualTo(firstDirection));
        }

        [Test]
        public void ExplosionEffect_DamagesUniqueTargetsAndExcludesMainTarget()
        {
            TestDamageTarget mainTarget = CreateTarget("ExplosionMain", Vector3.zero);
            TestDamageTarget nearbyTarget = CreateTarget("ExplosionNearby", Vector3.right);
            RecordingSpatialQuery spatialQuery = new(mainTarget, nearbyTarget, nearbyTarget);
            RecordingDamageProcessor damageProcessor = new();
            ExplodeOnHitEffectDefinitionSO effect = Track(
                ScriptableObject.CreateInstance<ExplodeOnHitEffectDefinitionSO>());

            effect.ExecuteEffect(new ProjectileEffectContext
            {
                TargetInstanceId = mainTarget.GetInstanceID(),
                DamageRequest = new DamageRequest
                {
                    AttackInstanceId = 7,
                    SourceInstanceId = 11,
                    DamageAmount = 12f,
                    DamageType = DamageType.Fire,
                    HitPosition = Vector3.zero,
                    HitNormal = Vector3.up
                },
                SpatialQueryService = spatialQuery,
                DamageProcessor = damageProcessor
            }, null);

            Assert.That(spatialQuery.LastRadius, Is.EqualTo(2.5f));
            Assert.That(damageProcessor.Targets, Has.Count.EqualTo(1));
            Assert.That(damageProcessor.Targets[0], Is.SameAs(nearbyTarget));
            Assert.That(damageProcessor.Requests[0].AttackInstanceId, Is.EqualTo(7));
        }

        [Test]
        public void KnockbackEffect_UsesEnemyMotionEffectTarget()
        {
            GameObject targetObject = Track(new GameObject("KnockbackTarget"));
            RecordingMotionTarget motionTarget = targetObject.AddComponent<RecordingMotionTarget>();
            KnockbackEffectDefinitionSO effect = Track(
                ScriptableObject.CreateInstance<KnockbackEffectDefinitionSO>());

            effect.ExecuteEffect(new ProjectileEffectContext
            {
                TargetGameObject = targetObject,
                DamageRequest = new DamageRequest
                {
                    ImpactDirection = Vector3.right
                }
            }, null);

            Assert.That(motionTarget.LastImpulse, Is.EqualTo(Vector3.right * 5f));
        }

        [Test]
        public void LaserTrajectory_UsesConfiguredRadiusForSweepQuery()
        {
            ProjectileProfileSO profile = Track(
                ProjectileProfileSO.CreateRuntimeProfile("LaserProfile"));
            profile.SpeedUnitsPerSecond = 10f;
            profile.MaximumTravelDistance = 10f;
            profile.CollisionRadius = 0.1f;
            profile.TrajectoryDefinition = Track(
                ScriptableObject.CreateInstance<LaserTrajectoryDefinitionSO>());
            RecordingCollisionQuery collisionQuery = new();
            ProjectileSimulationService simulation = new(
                new ProjectileViewPool(null),
                new ProjectileRuntimePool(),
                collisionQuery,
                null,
                null);
            ProjectileData projectile = new()
            {
                ProjectileProfile = profile,
                OriginPosition = Vector3.zero,
                LaunchDirection = Vector3.forward,
                DamageAmount = 1f,
                SpeedUnitsPerSecond = 10f,
                MaximumTravelDistance = 10f,
                CollisionRadius = 0.1f,
                DamageType = DamageType.Physical
            };

            try
            {
                Assert.That(simulation.TrySpawnProjectile(in projectile), Is.True);
                simulation.SimulateStep(0.01f);

                Assert.That(collisionQuery.SweepCallCount, Is.EqualTo(1));
                Assert.That(collisionQuery.LastRadius, Is.EqualTo(0.35f));
            }
            finally
            {
                simulation.Dispose();
            }
        }

        [Test]
        public void BoomerangTrajectory_ReturnsToOriginAndCompletes()
        {
            ProjectileProfileSO profile = Track(
                ProjectileProfileSO.CreateRuntimeProfile("BoomerangProfile"));
            profile.SpeedUnitsPerSecond = 1f;
            profile.MaximumTravelDistance = 2f;
            profile.TrajectoryDefinition = Track(
                ScriptableObject.CreateInstance<BoomerangTrajectoryDefinitionSO>());
            RecordingCollisionQuery collisionQuery = new();
            ProjectileSimulationService simulation = new(
                new ProjectileViewPool(null),
                new ProjectileRuntimePool(),
                collisionQuery,
                null,
                null);
            ProjectileData projectile = new()
            {
                ProjectileProfile = profile,
                OriginPosition = Vector3.zero,
                LaunchDirection = Vector3.forward,
                DamageAmount = 1f,
                SpeedUnitsPerSecond = 1f,
                MaximumTravelDistance = 2f,
                CollisionRadius = 0.1f,
                DamageType = DamageType.Physical
            };

            try
            {
                Assert.That(simulation.TrySpawnProjectile(in projectile), Is.True);
                simulation.SimulateStep(2f);
                simulation.SimulateStep(1.8f);
                simulation.SimulateStep(0.2f);

                Assert.That(collisionQuery.Directions[0], Is.EqualTo(Vector3.forward));
                Assert.That(collisionQuery.Directions[1].z, Is.LessThan(0f));
                Assert.That(simulation.ActiveProjectileCount, Is.Zero);
            }
            finally
            {
                simulation.Dispose();
            }
        }

        [Test]
        public void RuntimePool_ClearsAllMechanicStateBeforeReuse()
        {
            ProjectileProfileSO profile = Track(
                ProjectileProfileSO.CreateRuntimeProfile("PoolProfile"));
            TestDamageTarget target = CreateTarget("PooledTarget", Vector3.forward);
            ProjectileData data = new()
            {
                ProjectileProfile = profile,
                OriginPosition = Vector3.zero,
                LaunchDirection = Vector3.forward,
                SpeedUnitsPerSecond = 10f,
                MaximumTravelDistance = 10f
            };
            ProjectileRuntimePool pool = new();
            ProjectileRuntime first = pool.Rent(data, null);
            first.TrackingTarget = target;
            first.IsReturning = true;
            first.HasTriggeredExplosion = true;
            first.SimulationDeltaTime = 0.5f;
            first.HitTargetInstanceIds.Add(target.GetInstanceID());

            pool.Return(first);
            ProjectileRuntime reused = pool.Rent(data, null);

            Assert.That(reused.TrackingTarget, Is.Null);
            Assert.That(reused.IsReturning, Is.False);
            Assert.That(reused.HasTriggeredExplosion, Is.False);
            Assert.That(reused.SimulationDeltaTime, Is.Zero);
            Assert.That(reused.HitTargetInstanceIds, Is.Empty);
        }

        private T Track<T>(T target) where T : Object
        {
            _temporaryObjects.Add(target);
            return target;
        }

        private TestDamageTarget CreateTarget(string objectName, Vector3 position)
        {
            GameObject targetObject = Track(new GameObject(objectName));
            targetObject.transform.position = position;
            return targetObject.AddComponent<TestDamageTarget>();
        }

        private sealed class TestDamageTarget : MonoBehaviour, IDamageable
        {
            public Vector3 Position => transform.position;
            public bool IsAlive => true;
            public Transform Transform => transform;
        }

        private sealed class RecordingMotionTarget : MonoBehaviour, IEnemyMotionEffectTarget
        {
            public Vector3 LastImpulse { get; private set; }

            public void SetMovementSpeedMultiplier(float multiplier)
            {
            }

            public void AddExternalImpulse(Vector3 impulse)
            {
                LastImpulse = impulse;
            }
        }

        private sealed class RecordingDamageProcessor : IDamageRequestProcessor
        {
            public readonly List<IDamageable> Targets = new();
            public readonly List<DamageRequest> Requests = new();

            public bool TryApplyDamage(
                IDamageable target,
                in DamageRequest request,
                out DamageResult result)
            {
                Targets.Add(target);
                Requests.Add(request);
                result = new DamageResult
                {
                    Outcome = DamageOutcome.Applied,
                    AppliedDamage = request.DamageAmount
                };
                return true;
            }
        }

        private sealed class RecordingSpatialQuery : ISpatialQueryService
        {
            private readonly List<IDamageable> _targets;

            public float LastRadius { get; private set; }

            public RecordingSpatialQuery(params IDamageable[] targets)
            {
                _targets = new List<IDamageable>(targets);
            }

            public int Register(IDamageable entity, float radius, int layerMask) => -1;

            public void Unregister(int entityId)
            {
            }

            public void UpdatePosition(int entityId, Vector3 position)
            {
            }

            public IDamageable QueryNearest(Vector3 center, float radius, int layerMask)
            {
                return _targets.Count > 0 ? _targets[0] : null;
            }

            public List<IDamageable> QueryRadius(Vector3 center, float radius, int layerMask)
            {
                LastRadius = radius;
                return _targets;
            }

            public List<IDamageable> QueryAll(int layerMask)
            {
                return _targets;
            }
        }

        private sealed class RecordingCollisionQuery : IProjectileCollisionQuery
        {
            public readonly List<Vector3> Directions = new();
            public int SweepCallCount { get; private set; }
            public float LastRadius { get; private set; }

            public bool TryFindSweepCollision(
                ProjectileRuntime projectile,
                float travelDistance,
                out RaycastHit hit,
                out IDamageable target)
            {
                SweepCallCount++;
                Directions.Add(projectile.Direction);
                LastRadius = projectile.Data.CollisionRadius;
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
    }
}
