using System.Collections.Generic;
using System.Linq;
using CombatSystem;
using Interfaces;
using NUnit.Framework;
using SpatialIndexing;
using Services;
using UnityEditor;
using UnityEngine;

/// <summary>验证扩充后的道具目录、作用范围和散射弹发射节奏。</summary>
public sealed class ItemExpansionTests
{
    private const string CatalogPath = "Assets/_Project/SO/Item/ItemCatalog.asset";
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
    public void Catalog_ContainsTwentyFiveUniqueConfiguredItems()
    {
        ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);

        Assert.That(catalog, Is.Not.Null);
        Assert.That(catalog.Items, Has.Count.EqualTo(25));
        Assert.That(catalog.Items, Has.All.Not.Null);
        Assert.That(
            catalog.Items.Select(item => item.Id).Distinct().Count(),
            Is.EqualTo(25));

        foreach (ItemDefinition item in catalog.Items)
        {
            Assert.That(item.Price, Is.Zero, item.Id);
            Assert.That(item.CombatDefinition, Is.Not.Null, item.Id);
        }
    }

    [Test]
    public void ItemScopes_HaveSixteenTransmittersFourCentralAndFiveShared()
    {
        ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
        int transmitterOnly = 0;
        int centralOnly = 0;
        int shared = 0;

        foreach (ItemDefinition item in catalog.Items)
        {
            switch (item.CombatDefinition.CombatScope)
            {
                case CombatScope.Transmitter:
                    transmitterOnly++;
                    break;
                case CombatScope.Central:
                    centralOnly++;
                    break;
                case CombatScope.CentralAndTransmitter:
                    shared++;
                    break;
            }
        }

        Assert.That(transmitterOnly, Is.EqualTo(16));
        Assert.That(centralOnly, Is.EqualTo(4));
        Assert.That(shared, Is.EqualTo(5));
    }

    [Test]
    public void ScatterProfile_EmitsTenFasterBulletsAcrossOneAttackInterval()
    {
        ProjectileProfileSO profile = Track(ProjectileProfileSO.CreateRuntimeProfile("ScatterTest"));
        profile.EmitProjectilesSequentially = true;
        profile.SequentialProjectileCount = 10;
        profile.ProjectileSpeedMultiplier = 1.5f;
        profile.RandomDirectionAngleDegrees = 5f;

        RecordingSpatialQuery spatialQuery = new();
        ProjectileSimulationService simulation = new(
            null,
            new ProjectileRuntimePool(),
            new RecordingCollisionQuery(),
            new RecordingDamageProcessor(),
            null,
            spatialQuery);
        ProjectileData projectile = new()
        {
            ProjectileProfile = profile,
            OriginPosition = Vector3.zero,
            LaunchDirection = Vector3.forward,
            DamageAmount = 10f,
            SpeedUnitsPerSecond = 10f,
            MaximumTravelDistance = 100f,
            CollisionRadius = 0.1f,
            AttacksPerSecond = 2f,
            DamageType = DamageType.Physical
        };

        Random.State previousRandomState = Random.state;
        try
        {
            Random.InitState(1729);

            Assert.That(simulation.TrySpawnProjectileBatch(in projectile, 10), Is.True);
            Assert.That(simulation.ActiveProjectileCount, Is.EqualTo(1));

            simulation.SimulateStep(0.05f);
            Assert.That(simulation.ActiveProjectileCount, Is.EqualTo(1));

            simulation.SimulateStep(0.006f);
            Assert.That(simulation.ActiveProjectileCount, Is.EqualTo(2));

            simulation.SimulateStep(0.444f);
            Assert.That(simulation.ActiveProjectileCount, Is.EqualTo(10));
            Assert.That(spatialQuery.Projectiles, Has.Count.EqualTo(10));
            Assert.That(
                spatialQuery.Projectiles,
                Has.All.Property(nameof(ProjectileData.SpeedUnitsPerSecond)).EqualTo(15f));
            Assert.That(
                spatialQuery.Projectiles,
                Has.All.Matches<ProjectileData>(data =>
                    Vector3.Angle(data.LaunchDirection, Vector3.forward) <= 5f));
            Assert.That(
                spatialQuery.Projectiles.ConvertAll(data => data.DamageAmount),
                Has.All.EqualTo(10f));
        }
        finally
        {
            Random.state = previousRandomState;
            simulation.Dispose();
        }
    }

    private T Track<T>(T instance) where T : Object
    {
        _ownedObjects.Add(instance);
        return instance;
    }

    private sealed class RecordingCollisionQuery : IProjectileCollisionQuery
    {
        public bool TryFindSweepCollision(
            ProjectileRuntime projectile,
            float travelDistance,
            out RaycastHit hit,
            out IDamageable target)
        {
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

    private sealed class RecordingDamageProcessor : IDamageRequestProcessor
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

    private sealed class RecordingSpatialQuery : ISpatialQueryService, IProjectileSnapshotPublisher
    {
        public readonly List<ProjectileData> Projectiles = new();

        public IDamageable QueryNearest(Vector3 center, float radius, int layerMask) => null;

        public List<IDamageable> QueryRadius(Vector3 center, float radius, int layerMask) => new();

        public List<IDamageable> QueryAll(int layerMask) => new();

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
