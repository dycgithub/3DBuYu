using System.Collections.Generic;
using System.Linq;
using CombatSystem;
using Interfaces;
using NUnit.Framework;
using Services;
using UnityEngine;

/// <summary>验证投射物散射、速度变化、跳弹与分裂行为。</summary>
public sealed class ProjectileMechanicTests
{
    private readonly List<Object> _ownedObjects = new();

    [TearDown]
    public void TearDown()
    {
        for (int index = _ownedObjects.Count - 1; index >= 0; index--)
        {
            if (_ownedObjects[index] != null)
            {
                Object.DestroyImmediate(_ownedObjects[index]);
            }
        }

        _ownedObjects.Clear();
    }

    [Test]
    public void BatchSpawn_ScattersSymmetricallyAndVariesSpeedWithinConfiguredRange()
    {
        ProjectileProfileSO profile = CreateProfile();
        profile.ScatterAngleDegrees = 20f;
        profile.RandomSpeedVariance = 0.1f;
        RecordingSpatialQuery spatialQuery = new();
        ProjectileSimulationService simulation = CreateSimulation(
            new RecordingCollisionQuery(),
            spatialQuery,
            new RecordingDamageProcessor());
        Random.State previousRandomState = Random.state;

        try
        {
            Random.InitState(1729);
            ProjectileData projectile = CreateProjectile(profile);

            Assert.That(simulation.TrySpawnProjectileBatch(in projectile, 5), Is.True);

            Assert.That(spatialQuery.Projectiles, Has.Count.EqualTo(5));
            Assert.That(Vector3.Angle(spatialQuery.Projectiles[0].LaunchDirection, Vector3.forward), Is.EqualTo(20f).Within(0.01f));
            Assert.That(Vector3.Angle(spatialQuery.Projectiles[2].LaunchDirection, Vector3.forward), Is.EqualTo(0f).Within(0.01f));
            Assert.That(Vector3.Angle(spatialQuery.Projectiles[4].LaunchDirection, Vector3.forward), Is.EqualTo(20f).Within(0.01f));
            Assert.That(spatialQuery.Projectiles[0].LaunchDirection.x, Is.LessThan(0f));
            Assert.That(spatialQuery.Projectiles[4].LaunchDirection.x, Is.GreaterThan(0f));
            Assert.That(spatialQuery.Projectiles, Has.All.Property(nameof(ProjectileData.SpeedUnitsPerSecond)).InRange(9f, 11f));
            Assert.That(spatialQuery.Projectiles.ConvertAll(data => data.SpeedUnitsPerSecond).Distinct().Count(), Is.GreaterThan(1));
        }
        finally
        {
            Random.state = previousRandomState;
            simulation.Dispose();
        }
    }

    [Test]
    public void Hit_RicochetsToNearestUnhitTargetAndReducesDamage()
    {
        ProjectileProfileSO profile = CreateProfile();
        profile.RicochetCount = 1;
        profile.RicochetSearchRadius = 5f;
        profile.RicochetDamageMultiplier = 0.5f;
        FakeDamageable firstTarget = CreateTarget(Vector3.forward);
        FakeDamageable nextTarget = CreateTarget(Vector3.right * 2f);
        RecordingSpatialQuery spatialQuery = new(firstTarget, nextTarget);
        RecordingDamageProcessor damageProcessor = new();
        RecordingCollisionQuery collisionQuery = new(firstTarget, nextTarget);
        ProjectileSimulationService simulation = CreateSimulation(
            collisionQuery,
            spatialQuery,
            damageProcessor);
        ProjectileData projectile = CreateProjectile(profile);

        try
        {
            Assert.That(simulation.TrySpawnProjectile(in projectile), Is.True);
            simulation.SimulateStep(0.01f);

            Assert.That(simulation.ActiveProjectileCount, Is.EqualTo(1));
            Assert.That(spatialQuery.Projectiles[0].LaunchDirection, Is.EqualTo(Vector3.right));
            Assert.That(spatialQuery.Projectiles[0].DamageAmount, Is.EqualTo(5f));

            simulation.SimulateStep(0.01f);

            Assert.That(simulation.ActiveProjectileCount, Is.Zero);
            Assert.That(damageProcessor.Requests.ConvertAll(request => request.DamageAmount), Is.EqualTo(new[] { 10f, 5f }));
        }
        finally
        {
            simulation.Dispose();
        }
    }

    [Test]
    public void Hit_SplitsIntoThreeChildrenAndChildrenCannotSplitAgain()
    {
        ProjectileProfileSO profile = CreateProfile();
        profile.SplitProjectileCount = 3;
        profile.SplitAngleDegrees = 30f;
        profile.SplitTargetSearchRadius = 8f;
        FakeDamageable hitTarget = CreateTarget(Vector3.forward);
        FakeDamageable rightTarget = CreateTarget(Vector3.right * 2f);
        FakeDamageable upTarget = CreateTarget(Vector3.up * 3f);
        RecordingSpatialQuery spatialQuery = new(hitTarget, rightTarget, upTarget, rightTarget);
        RecordingDamageProcessor damageProcessor = new();
        RecordingCollisionQuery collisionQuery = new(hitTarget, rightTarget, rightTarget, rightTarget);
        ProjectileSimulationService simulation = CreateSimulation(
            collisionQuery,
            spatialQuery,
            damageProcessor);
        ProjectileData projectile = CreateProjectile(profile);

        try
        {
            Assert.That(simulation.TrySpawnProjectile(in projectile), Is.True);
            simulation.SimulateStep(0.01f);

            Assert.That(simulation.ActiveProjectileCount, Is.EqualTo(3));
            Assert.That(spatialQuery.Projectiles, Has.All.Property(nameof(ProjectileData.IsSplitChild)).True);
            Assert.That(spatialQuery.QueryRadiusCount, Is.EqualTo(1));
            Assert.That(Vector3.Distance(spatialQuery.Projectiles[0].LaunchDirection, spatialQuery.Projectiles[1].LaunchDirection), Is.GreaterThan(0.1f));

            simulation.SimulateStep(0.01f);

            Assert.That(simulation.ActiveProjectileCount, Is.Zero);
            Assert.That(spatialQuery.QueryRadiusCount, Is.EqualTo(1));
            Assert.That(damageProcessor.Requests, Has.Count.EqualTo(4));
        }
        finally
        {
            simulation.Dispose();
        }
    }

    [Test]
    public void SplitWithoutNearbyTargets_UsesFanDirections()
    {
        ProjectileProfileSO profile = CreateProfile();
        profile.SplitProjectileCount = 3;
        profile.SplitAngleDegrees = 30f;
        FakeDamageable hitTarget = CreateTarget(Vector3.forward);
        RecordingSpatialQuery spatialQuery = new();
        RecordingCollisionQuery collisionQuery = new(hitTarget);
        ProjectileSimulationService simulation = CreateSimulation(
            collisionQuery,
            spatialQuery,
            new RecordingDamageProcessor());
        ProjectileData projectile = CreateProjectile(profile);

        try
        {
            Assert.That(simulation.TrySpawnProjectile(in projectile), Is.True);
            simulation.SimulateStep(0.01f);

            Assert.That(spatialQuery.Projectiles, Has.Count.EqualTo(3));
            Assert.That(Vector3.Angle(spatialQuery.Projectiles[0].LaunchDirection, spatialQuery.Projectiles[2].LaunchDirection), Is.EqualTo(60f).Within(0.01f));
            Assert.That(spatialQuery.Projectiles[0].IsSplitChild, Is.True);
            Assert.That(spatialQuery.Projectiles[1].IsSplitChild, Is.True);
            Assert.That(spatialQuery.Projectiles[2].IsSplitChild, Is.True);
        }
        finally
        {
            simulation.Dispose();
        }
    }

    private ProjectileProfileSO CreateProfile()
    {
        ProjectileProfileSO profile = Track(ProjectileProfileSO.CreateRuntimeProfile("ProjectileMechanicTest"));
        profile.MaximumTravelDistance = 100f;
        return profile;
    }

    private ProjectileData CreateProjectile(ProjectileProfileSO profile)
    {
        return new ProjectileData
        {
            ProjectileProfile = profile,
            OriginPosition = Vector3.zero,
            LaunchDirection = Vector3.forward,
            DamageAmount = 10f,
            SpeedUnitsPerSecond = 10f,
            MaximumTravelDistance = 100f,
            CollisionRadius = 0.1f,
            DamageType = DamageType.Physical
        };
    }

    private ProjectileSimulationService CreateSimulation(
        RecordingCollisionQuery collisionQuery,
        RecordingSpatialQuery spatialQuery,
        RecordingDamageProcessor damageProcessor)
    {
        return new ProjectileSimulationService(
            new ProjectileViewPool(null),
            new ProjectileRuntimePool(),
            collisionQuery,
            damageProcessor,
            null,
            spatialQuery);
    }

    private FakeDamageable CreateTarget(Vector3 position)
    {
        GameObject targetObject = Track(new GameObject("ProjectileMechanicTarget"));
        targetObject.transform.position = position;
        return new FakeDamageable(targetObject.transform);
    }

    private T Track<T>(T instance) where T : Object
    {
        _ownedObjects.Add(instance);
        return instance;
    }

    private sealed class FakeDamageable : IDamageable
    {
        public Vector3 Position => Transform.position;
        public bool IsAlive => true;
        public Transform Transform { get; }

        public FakeDamageable(Transform transform)
        {
            Transform = transform;
        }
    }

    private sealed class RecordingCollisionQuery : IProjectileCollisionQuery
    {
        private readonly Queue<IDamageable> _targets;

        public RecordingCollisionQuery(params IDamageable[] targets)
        {
            _targets = new Queue<IDamageable>(targets);
        }

        public bool TryFindSweepCollision(
            ProjectileRuntime projectile,
            float travelDistance,
            out RaycastHit hit,
            out IDamageable target)
        {
            hit = default;
            target = _targets.Count > 0 ? _targets.Dequeue() : null;
            return target != null;
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

    private sealed class RecordingDamageProcessor : IDamageRequestProcessor
    {
        public readonly List<DamageRequest> Requests = new();

        public bool TryApplyDamage(
            IDamageable target,
            in DamageRequest request,
            out DamageResult result)
        {
            Requests.Add(request);
            result = new DamageResult
            {
                Outcome = DamageOutcome.Applied,
                AppliedDamage = request.DamageAmount
            };
            return true;
        }
    }

    private sealed class RecordingSpatialQuery : ISpatialQueryService, IProjectileSnapshotPublisher
    {
        private readonly List<IDamageable> _targets;

        public readonly List<ProjectileData> Projectiles = new();
        public int QueryRadiusCount { get; private set; }

        public RecordingSpatialQuery(params IDamageable[] targets)
        {
            _targets = new List<IDamageable>(targets);
        }

        public IDamageable QueryNearest(Vector3 center, float radius, int layerMask)
        {
            return _targets.Count > 0 ? _targets[0] : null;
        }

        public List<IDamageable> QueryRadius(Vector3 center, float radius, int layerMask)
        {
            QueryRadiusCount++;
            return _targets;
        }

        public List<IDamageable> QueryAll(int layerMask)
        {
            return _targets;
        }

        public void PublishProjectiles(IReadOnlyList<ProjectileRuntime> projectiles)
        {
            Projectiles.Clear();
            for (int index = 0; index < projectiles.Count; index++)
            {
                Projectiles.Add(projectiles[index].Data);
            }
        }
    }
}
