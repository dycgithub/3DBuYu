using System;
using System.Collections.Generic;
using CombatSystem;
using Interfaces;
using NUnit.Framework;
using UnityEngine;
using Utils;

/// <summary>验证模拟服务发布的真实弹体视图在发射后保持激活。</summary>
public sealed class ProjectileSimulationServiceTests
{
    private GameObject _prefab;
    private ProjectileVisualDefinitionSO _visual;
    private ProjectileTrajectoryDefinitionSO _trajectory;
    private ProjectileProfileSO _profile;
    private HitOnceQuery _collisionQuery;
    private TrackingPool _gameObjectPool;
    private ProjectileViewPool _viewPool;
    private ProjectileStatePool _statePool;
    private ProjectileSimulationService _simulation;

    /// <summary>连接最小的真实弹体预制体、对象池与模拟服务。</summary>
    [SetUp]
    public void SetUp()
    {
        _prefab = new GameObject("ProjectileSimulationTestPrefab");
        _prefab.AddComponent<TrailRenderer>();
        GameObject body = new("Body");
        body.transform.SetParent(_prefab.transform, false);

        _visual = ScriptableObject.CreateInstance<ProjectileVisualDefinitionSO>();
        _visual.ProjectilePrefab = _prefab;
        _profile = ScriptableObject.CreateInstance<ProjectileProfileSO>();
        _profile.VisualDefinition = _visual;
        _trajectory = ScriptableObject.CreateInstance<ProjectileTrajectoryDefinitionSO>();
        _profile.TrajectoryDefinition = _trajectory;

        _gameObjectPool = new TrackingPool();
        _viewPool = new ProjectileViewPool(_gameObjectPool);
        _statePool = new ProjectileStatePool();
        _collisionQuery = new HitOnceQuery();
        _simulation = new ProjectileSimulationService(
            _viewPool,
            _statePool,
            _collisionQuery,
            null,
            null);
    }

    /// <summary>释放活动弹体、对象池实例与测试配置。</summary>
    [TearDown]
    public void TearDown()
    {
        _simulation?.Dispose();
        _viewPool?.Dispose();
        _statePool?.Dispose();
        _gameObjectPool?.Dispose();

        if (_profile != null)
        {
            UnityEngine.Object.DestroyImmediate(_profile);
        }

        if (_trajectory != null)
        {
            UnityEngine.Object.DestroyImmediate(_trajectory);
        }

        if (_visual != null)
        {
            UnityEngine.Object.DestroyImmediate(_visual);
        }

        if (_prefab != null)
        {
            UnityEngine.Object.DestroyImmediate(_prefab);
        }
    }

    /// <summary>直接发射请求被模拟服务接收时，发布的视图必须可见。</summary>
    [Test]
    public void DirectLaunchActivatesPublishedView()
    {
        ProjectileData projectile = CreateProjectileData();

        Assert.That(_simulation.TrySpawnProjectile(in projectile), Is.True);
        Assert.That(_simulation.ActiveProjectileCount, Is.EqualTo(1));
        Assert.That(_gameObjectPool.RentedInstances, Has.Count.EqualTo(1));
        Assert.That(_gameObjectPool.RentedInstances[0].activeSelf, Is.True);
    }

    /// <summary>顺序发射的首发与后续视图都在进入活动列表时激活。</summary>
    [Test]
    public void SequentialLaunchActivatesEveryPublishedView()
    {
        _profile.EmitProjectilesSequentially = true;
        _profile.SequentialProjectileCount = 3;
        ProjectileData projectile = CreateProjectileData();
        projectile.AttacksPerSecond = 1f;

        Assert.That(_simulation.TrySpawnProjectile(in projectile), Is.True);
        Assert.That(_gameObjectPool.RentedInstances[0].activeSelf, Is.True);

        _simulation.SimulateStep(0.5f);
        _simulation.SimulateStep(0.5f);

        Assert.That(_simulation.ActiveProjectileCount, Is.EqualTo(3));
        Assert.That(_gameObjectPool.RentedInstances, Has.Count.EqualTo(3));
        foreach (GameObject instance in _gameObjectPool.RentedInstances)
        {
            Assert.That(instance.activeSelf, Is.True);
        }
    }

    /// <summary>模拟步发布的分裂子弹视图必须激活。</summary>
    [Test]
    public void SplitLaunchActivatesPublishedView()
    {
        _profile.SplitProjectileCount = 1;
        ProjectileData projectile = CreateProjectileData();

        Assert.That(_simulation.TrySpawnProjectile(in projectile), Is.True);
        _collisionQuery.HitNextSweep = true;
        _simulation.SimulateStep(0.1f);

        Assert.That(_simulation.ActiveProjectileCount, Is.EqualTo(1));
        Assert.That(_gameObjectPool.RentedInstances, Has.Count.EqualTo(2));
        Assert.That(_gameObjectPool.RentedInstances[1].activeSelf, Is.True);
    }

    /// <summary>准备批次保持隐藏，只有 Commit 才发布并激活视图。</summary>
    [Test]
    public void PreparedLaunchWaitsForCommit()
    {
        ProjectileData projectile = CreateProjectileData();

        Assert.That(_simulation.TryPrepare(new[] { projectile }, out IPreparedProjectileBatch batch), Is.True);
        Assert.That(_simulation.ActiveProjectileCount, Is.Zero);
        Assert.That(_gameObjectPool.RentedInstances[0].activeSelf, Is.False);

        batch.Commit();

        Assert.That(_simulation.ActiveProjectileCount, Is.EqualTo(1));
        Assert.That(_gameObjectPool.RentedInstances[0].activeSelf, Is.True);
    }

    /// <summary>构造足以经过真实单发生成管线的投射物数据。</summary>
    /// <returns>具有有限射程、正速度和普通轨迹的数据。</returns>
    private ProjectileData CreateProjectileData()
    {
        return new ProjectileData
        {
            ProjectileProfile = _profile,
            OriginPosition = Vector3.zero,
            LaunchDirection = Vector3.forward,
            DamageAmount = 1f,
            SpeedUnitsPerSecond = 1f,
            MaximumTravelDistance = 20f,
            CollisionRadius = 0.1f
        };
    }

    /// <summary>提供一次可控命中以穿过分裂子弹的真实生成链路。</summary>
    private sealed class HitOnceQuery : IProjectileCollisionQuery
    {
        private readonly IDamageable _target = new TestDamageable();

        internal bool HitNextSweep;

        public bool TryFindSweepCollision(
            ProjectileState projectile,
            float travelDistance,
            out RaycastHit hit,
            out IDamageable target)
        {
            hit = default;
            target = null;
            if (!HitNextSweep)
            {
                return false;
            }

            HitNextSweep = false;
            target = _target;
            return true;
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

    /// <summary>满足命中处理所需的最小无场景目标。</summary>
    private sealed class TestDamageable : IDamageable
    {
        public Vector3 Position => Vector3.zero;

        public bool IsAlive => true;

        public Transform Transform => null;
    }

    /// <summary>记录租借实例并将对象池操作委托给项目真实实现。</summary>
    private sealed class TrackingPool : IGameObjectPool
    {
        private readonly GameObjectPoolService _inner = new();

        internal readonly List<GameObject> RentedInstances = new();

        /// <summary>记录池返回的实例，以公开视图状态断言发射行为。</summary>
        public GameObject Rent(
            GameObject prefab,
            PoolSettings settings,
            Transform parent = null,
            bool activate = true,
            Func<GameObject, GameObject> factory = null)
        {
            GameObject instance = _inner.Rent(prefab, settings, parent, activate, factory);
            RentedInstances.Add(instance);
            return instance;
        }

        public bool Return(GameObject instance) => _inner.Return(instance);

        public void Prewarm(
            GameObject prefab,
            PoolSettings settings,
            int count,
            Func<GameObject, GameObject> factory = null)
        {
            _inner.Prewarm(prefab, settings, count, factory);
        }

        public PoolUsage GetUsage(GameObject prefab) => _inner.GetUsage(prefab);

        public void Clear(GameObject prefab) => _inner.Clear(prefab);

        public void ClearAll() => _inner.ClearAll();

        public void Dispose() => _inner.Dispose();
    }
}
