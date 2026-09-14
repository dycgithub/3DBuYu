using System;
using System.Reflection;
using EnemySystem;
using GameSystem;
using InventorySystem.Shop;
using NUnit.Framework;
using CombatSystem;
using Services;
using UnityEngine;
using Object = UnityEngine.Object;
using Utils;
using VContainer;
using VContainer.Unity;

public class EnergyServiceTests
{
    private EnergyService _energy;

    [SetUp]
    public void SetUp()
    {
        _energy = new EnergyService();
    }

    [TearDown]
    public void TearDown()
    {
        _energy.Dispose();
    }

    [Test]
    public void Initialize_ClampsEnergyToMaximum()
    {
        _energy.Initialize(150f, 100f);

        Assert.That(_energy.MaximumEnergy, Is.EqualTo(100f));
        Assert.That(_energy.CurrentEnergy.CurrentValue, Is.EqualTo(100f));
    }

    [Test]
    public void TrySpend_RejectsInsufficientEnergyAtomically()
    {
        _energy.Initialize(100f, 100f);

        bool firstSpend = _energy.TrySpend(20f, EnergySpendKind.Shot);
        bool rejectedSpend = _energy.TrySpend(81f, EnergySpendKind.Skill);

        Assert.That(firstSpend, Is.True);
        Assert.That(rejectedSpend, Is.False);
        Assert.That(_energy.CurrentEnergy.CurrentValue, Is.EqualTo(80f));
    }

    [Test]
    public void Tick_DrainsEnergyAndClampsAtZero()
    {
        _energy.Initialize(10f, 10f);

        float drained = _energy.Tick(2f, 2f);

        Assert.That(drained, Is.EqualTo(4f));
        Assert.That(_energy.CurrentEnergy.CurrentValue, Is.EqualTo(6f));
        Assert.That(_energy.IsDepleted, Is.False);
    }

    [Test]
    public void AddEnergy_ReturnsOnlyTheAmountThatFitsWithinTheCap()
    {
        _energy.Initialize(5f, 10f);

        float gained = _energy.AddEnergy(20f);

        Assert.That(gained, Is.EqualTo(5f));
        Assert.That(_energy.CurrentEnergy.CurrentValue, Is.EqualTo(10f));
    }
}

public class GamePauseServiceTests
{
    private float _originalTimeScale;
    private GamePauseService _pauseService;

    [SetUp]
    public void SetUp()
    {
        _originalTimeScale = Time.timeScale;
        _pauseService = new GamePauseService();
    }

    [TearDown]
    public void TearDown()
    {
        _pauseService.Dispose();
        Time.timeScale = _originalTimeScale;
    }

    [Test]
    public void PauseAndResume_RestoresTheTimeScaleThatExistedBeforePause()
    {
        Time.timeScale = 0.35f;

        _pauseService.Pause();
        Assert.That(_pauseService.IsPaused, Is.True);
        Assert.That(Time.timeScale, Is.Zero);

        _pauseService.Resume();
        Assert.That(_pauseService.IsPaused, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(0.35f));
    }

    [Test]
    public void RepeatedPauseAndResume_OnlyPublishesActualStateChanges()
    {
        int eventCount = 0;
        _pauseService.PauseStateChanged += _ => eventCount++;

        _pauseService.Pause();
        _pauseService.Pause();
        _pauseService.Resume();
        _pauseService.Resume();

        Assert.That(eventCount, Is.EqualTo(2));
    }
}

public class GameObjectPoolServiceTests
{
    private GameObjectPoolService _pool;
    private GameObject _prefab;
    private GameObject _first;
    private GameObject _second;

    [SetUp]
    public void SetUp()
    {
        _pool = new GameObjectPoolService();
        _prefab = new GameObject("PoolTestPrefab");
    }

    [TearDown]
    public void TearDown()
    {
        _pool.Dispose();
        DestroyImmediateIfPresent(_first);
        DestroyImmediateIfPresent(_second);
        DestroyImmediateIfPresent(_prefab);
    }

    [Test]
    public void ReturnThenRent_ReusesTheSameInstanceAndTracksOccupancy()
    {
        var settings = new PoolSettings(initialCapacity: 1, maximumRetained: 2);

        _first = _pool.Rent(_prefab, settings);
        PoolUsage rentedUsage = _pool.GetUsage(_prefab);
        bool returned = _pool.Return(_first);
        PoolUsage availableUsage = _pool.GetUsage(_prefab);
        _second = _pool.Rent(_prefab, settings);

        Assert.That(rentedUsage.TotalCount, Is.EqualTo(1));
        Assert.That(rentedUsage.RentedCount, Is.EqualTo(1));
        Assert.That(returned, Is.True);
        Assert.That(availableUsage.AvailableCount, Is.EqualTo(1));
        Assert.That(_second, Is.SameAs(_first));
    }

    [Test]
    public void Return_DisablesObjectBeforeInvokingReturnCallbacks()
    {
        _prefab.AddComponent<PoolLifecycleProbe>();
        _first = _pool.Rent(
            _prefab,
            new PoolSettings(initialCapacity: 0, maximumRetained: 1));
        PoolLifecycleProbe probe = _first.GetComponent<PoolLifecycleProbe>();

        bool returned = _pool.Return(_first);

        Assert.That(returned, Is.True);
        Assert.That(probe.ReturnObservedInactive, Is.True);
    }

    [Test]
    public void RepeatedReturn_IsRejectedAndDoesNotDuplicateTheAvailableEntry()
    {
        var settings = new PoolSettings(initialCapacity: 0, maximumRetained: 1);
        _first = _pool.Rent(_prefab, settings);

        bool firstReturn = _pool.Return(_first);
        bool repeatedReturn = _pool.Return(_first);
        PoolUsage usage = _pool.GetUsage(_prefab);

        Assert.That(firstReturn, Is.True);
        Assert.That(repeatedReturn, Is.False);
        Assert.That(usage.AvailableCount, Is.EqualTo(1));
    }

    [Test]
    public void ConflictingSettings_ThrowsBeforeChangingExistingBucket()
    {
        PoolSettings initialSettings = new(initialCapacity: 1, maximumRetained: 2);
        _first = _pool.Rent(_prefab, initialSettings);
        PoolUsage beforeConflict = _pool.GetUsage(_prefab);

        Assert.Throws<InvalidOperationException>(() =>
        {
            _pool.Rent(_prefab, new PoolSettings(initialCapacity: 0, maximumRetained: 3));
        });

        PoolUsage afterConflict = _pool.GetUsage(_prefab);
        Assert.That(afterConflict.TotalCount, Is.EqualTo(beforeConflict.TotalCount));
        Assert.That(afterConflict.AvailableCount, Is.EqualTo(beforeConflict.AvailableCount));
        Assert.That(afterConflict.RentedCount, Is.EqualTo(beforeConflict.RentedCount));
        Assert.That(_pool.Return(_first), Is.True);
    }

    [Test]
    public void ConflictingFactory_ThrowsBeforeInvokingTheNewFactory()
    {
        int firstFactoryCalls = 0;
        int conflictingFactoryCalls = 0;
        Func<GameObject, GameObject> firstFactory = prefab =>
        {
            firstFactoryCalls++;
            return Object.Instantiate(prefab);
        };
        Func<GameObject, GameObject> conflictingFactory = prefab =>
        {
            conflictingFactoryCalls++;
            return Object.Instantiate(prefab);
        };

        _first = _pool.Rent(
            _prefab,
            new PoolSettings(initialCapacity: 0, maximumRetained: 2),
            factory: firstFactory);

        Assert.Throws<InvalidOperationException>(() =>
        {
            _pool.Rent(
                _prefab,
                new PoolSettings(initialCapacity: 0, maximumRetained: 2),
                factory: conflictingFactory);
        });

        Assert.That(firstFactoryCalls, Is.EqualTo(1));
        Assert.That(conflictingFactoryCalls, Is.Zero);
    }

    [Test]
    public void ConflictingPrewarmSettings_ThrowsBeforeChangingExistingBucket()
    {
        PoolSettings initialSettings = new(initialCapacity: 1, maximumRetained: 2);
        _pool.Prewarm(_prefab, initialSettings, count: 1);
        PoolUsage beforeConflict = _pool.GetUsage(_prefab);

        Assert.Throws<InvalidOperationException>(() =>
        {
            _pool.Prewarm(
                _prefab,
                new PoolSettings(initialCapacity: 2, maximumRetained: 4),
                count: 1);
        });

        PoolUsage afterConflict = _pool.GetUsage(_prefab);
        Assert.That(afterConflict.TotalCount, Is.EqualTo(beforeConflict.TotalCount));
        Assert.That(afterConflict.AvailableCount, Is.EqualTo(beforeConflict.AvailableCount));
        Assert.That(afterConflict.RentedCount, Is.EqualTo(beforeConflict.RentedCount));
    }

    [Test]
    public void Clear_AllowsPrefabToUseANewConfiguration()
    {
        _pool.Prewarm(
            _prefab,
            new PoolSettings(initialCapacity: 1, maximumRetained: 1),
            count: 1);
        _pool.Clear(_prefab);

        _first = _pool.Rent(
            _prefab,
            new PoolSettings(initialCapacity: 0, maximumRetained: 3));

        Assert.That(_first, Is.Not.Null);
        Assert.That(_pool.GetUsage(_prefab).RentedCount, Is.EqualTo(1));
    }

    [Test]
    public void ProjectCoreInstaller_RegistersOnePoolForBothServiceContracts()
    {
        var builder = new ContainerBuilder();
        new ProjectCoreInstaller().Install(builder);

        IObjectResolver container = builder.Build();
        try
        {
            Assert.That(
                container.Resolve<IGameObjectPool>(),
                Is.SameAs(container.Resolve<GameObjectPoolService>()));
        }
        finally
        {
            container.Dispose();
        }
    }

    [Test]
    public void ProjectScope_ProvidesOnePoolInstanceToChildScope()
    {
        GameObject scopeObject = new GameObject("ProjectScopePoolContract");
        scopeObject.SetActive(false);
        scopeObject.AddComponent<ShopManager>();
        scopeObject.AddComponent<ResourceManager>();
        scopeObject.AddComponent<AudioManager>();
        SceneLoader sceneLoader = scopeObject.AddComponent<SceneLoader>();
        sceneLoader.autoLoadOnStart = false;
        ProjectLifetimeScope projectScope = scopeObject.AddComponent<ProjectLifetimeScope>();
        projectScope.autoRun = false;
        LifetimeScope childScope = null;

        try
        {
            using (LifetimeScope.EnqueueParent(null))
            {
                FieldInfo scopeName = typeof(LifetimeScope).GetField(
                    "scopeName",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(scopeName, Is.Not.Null);
                scopeName.SetValue(projectScope, "ProjectScopePoolContract");
                projectScope.Build();
                scopeObject.SetActive(true);
            }

            childScope = projectScope.CreateChild(childScopeName: "ProjectScopePoolContractChild");
            FieldInfo childScopeName = typeof(LifetimeScope).GetField(
                "scopeName",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(childScopeName, Is.Not.Null);
            childScopeName.SetValue(childScope, "ProjectScopePoolContractChild");
            childScope.Build();

            Assert.That(
                projectScope.Container.Resolve<IGameObjectPool>(),
                Is.SameAs(projectScope.Container.Resolve<GameObjectPoolService>()));
            Assert.That(
                childScope.Container.Resolve<IGameObjectPool>(),
                Is.SameAs(projectScope.Container.Resolve<IGameObjectPool>()));
        }
        finally
        {
            if (childScope != null)
            {
                Object.DestroyImmediate(childScope.gameObject);
            }

            Object.DestroyImmediate(scopeObject);
        }
    }

    private static void DestroyImmediateIfPresent(GameObject instance)
    {
        if (instance != null)
            Object.DestroyImmediate(instance);
    }

    private sealed class PoolLifecycleProbe : MonoBehaviour, IPooledObject
    {
        public bool ReturnObservedInactive { get; private set; }

        public void OnRentFromPool()
        {
        }

        public void OnReturnToPool()
        {
            ReturnObservedInactive = !gameObject.activeSelf;
        }
    }
}

public class EnemyDamageInterceptorTests
{
    private GameObject _enemyObject;

    [TearDown]
    public void TearDown()
    {
        if (_enemyObject != null)
            Object.DestroyImmediate(_enemyObject);
    }

    [Test]
    public void RegisterPreDamageInterceptor_IgnoresDuplicatesAndSupportsRemoval()
    {
        _enemyObject = new GameObject("EnemyDamageInterceptorTest");
        _enemyObject.AddComponent<BoxCollider>();
        Enemy enemy = _enemyObject.AddComponent<Enemy>();
        int invokeCount = 0;
        Enemy.EnemyDamageInterceptor interceptor =
            (Enemy target, float originalDamage, ref float finalDamage) =>
            {
                invokeCount++;
                return false;
            };

        enemy.RegisterPreDamageInterceptor(interceptor);
        enemy.RegisterPreDamageInterceptor(interceptor);
        DamageResult result = enemy.ReceiveDamageRequest(new DamageRequest { DamageAmount = 1f });

        Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Dodged));
        Assert.That(invokeCount, Is.EqualTo(1));

        enemy.UnregisterPreDamageInterceptor(interceptor);
        enemy.ReceiveDamageRequest(new DamageRequest { DamageAmount = 0f });

        Assert.That(invokeCount, Is.EqualTo(1));
    }
}

public class GameSessionTests
{
    [Test]
    public void BeginRunAndRecordKill_KeepRunDataSeparateFromPermanentCurrency()
    {
        var session = new GameSession();

        session.BeginRun("hard", 1234);
        session.RecordKill(25);
        session.RecordKill(0);
        session.AdvanceTime(310f);

        Assert.That(session.DifficultyId, Is.EqualTo("hard"));
        Assert.That(session.RandomSeed, Is.EqualTo(1234));
        Assert.That(session.KillCount, Is.EqualTo(2));
        Assert.That(session.PendingPoints, Is.EqualTo(25));
        Assert.That(session.ElapsedTime, Is.EqualTo(310f));
    }

    [Test]
    public void Reset_ClearsAllRunScopedData()
    {
        var session = new GameSession();
        session.BeginRun("hard", 1234);
        session.RecordKill(25);
        session.AdvanceTime(310f);

        session.Reset();

        Assert.That(session.PendingPoints, Is.Zero);
        Assert.That(session.KillCount, Is.Zero);
        Assert.That(session.ElapsedTime, Is.Zero);
        Assert.That(session.DifficultyId, Is.Empty);
        Assert.That(session.RandomSeed, Is.Zero);
    }

    [Test]
    public void AdvanceTime_IgnoresNonFiniteInput()
    {
        var session = new GameSession();

        session.AdvanceTime(float.NaN);
        session.AdvanceTime(float.PositiveInfinity);

        Assert.That(session.ElapsedTime, Is.Zero);
    }
}
