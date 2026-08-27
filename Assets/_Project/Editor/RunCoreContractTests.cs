using EnemySystem;
using GameSystem;
using NUnit.Framework;
using CombatSystem;
using Services;
using UnityEngine;
using Utils;

public class CombatEnergyServiceTests
{
    private CombatEnergyService _energy;

    [SetUp]
    public void SetUp()
    {
        _energy = new CombatEnergyService();
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
        Assert.That(_energy.CostMultiplier, Is.EqualTo(1f));
    }

    [Test]
    public void TrySpend_AppliesTheConfiguredMultiplierAtomically()
    {
        _energy.Initialize(100f, 100f);
        _energy.SetCostMultiplier(2f);

        bool firstSpend = _energy.TrySpend(20f, EnergySpendKind.Shot);
        bool rejectedSpend = _energy.TrySpend(31f, EnergySpendKind.Skill);

        Assert.That(firstSpend, Is.True);
        Assert.That(rejectedSpend, Is.False);
        Assert.That(_energy.CurrentEnergy.CurrentValue, Is.EqualTo(60f));
    }

    [Test]
    public void Tick_DrainsEnergyAndClampsAtZero()
    {
        _energy.Initialize(10f, 10f);
        _energy.SetCostMultiplier(3f);

        float drained = _energy.Tick(2f, 2f);

        Assert.That(drained, Is.EqualTo(10f));
        Assert.That(_energy.CurrentEnergy.CurrentValue, Is.EqualTo(0f));
        Assert.That(_energy.IsDepleted, Is.True);
    }

    [Test]
    public void SetCostMultiplier_DoesNotAllowValuesBelowOne()
    {
        _energy.Initialize(10f, 10f);

        _energy.SetCostMultiplier(0f);

        Assert.That(_energy.CostMultiplier, Is.EqualTo(1f));
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

public class RunRuleMathTests
{
    [TestCase(300f, 300f, 0)]
    [TestCase(304.99f, 300f, 0)]
    [TestCase(305f, 300f, 1)]
    [TestCase(310f, 300f, 2)]
    public void GetOvertimeTier_UsesFiveSecondSteps(float elapsedTime, float targetDuration, int expectedTier)
    {
        Assert.That(RunRuleMath.GetOvertimeTier(elapsedTime, targetDuration), Is.EqualTo(expectedTier));
    }

    [Test]
    public void GetOvertimeMultiplier_IncreasesByOneForEachTier()
    {
        Assert.That(RunRuleMath.GetOvertimeMultiplier(315f, 300f), Is.EqualTo(4f));
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

    private static void DestroyImmediateIfPresent(GameObject instance)
    {
        if (instance != null)
            Object.DestroyImmediate(instance);
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
        DamageResult result = enemy.ReceiveDamage(new DamageRequest { BaseDamage = 1f });

        Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Dodged));
        Assert.That(invokeCount, Is.EqualTo(1));

        enemy.UnregisterPreDamageInterceptor(interceptor);
        enemy.ReceiveDamage(new DamageRequest { BaseDamage = 0f });

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
        session.AdvanceTime(310f, 300f);

        Assert.That(session.DifficultyId, Is.EqualTo("hard"));
        Assert.That(session.RandomSeed, Is.EqualTo(1234));
        Assert.That(session.KillCount, Is.EqualTo(2));
        Assert.That(session.PendingPoints, Is.EqualTo(25));
        Assert.That(session.ElapsedTime, Is.EqualTo(310f));
        Assert.That(session.OvertimeTier, Is.EqualTo(2));
        Assert.That(session.OvertimeMultiplier, Is.EqualTo(3f));
    }

    [Test]
    public void Reset_ClearsAllRunScopedData()
    {
        var session = new GameSession();
        session.BeginRun("hard", 1234);
        session.RecordKill(25);
        session.AdvanceTime(310f, 300f);

        session.Reset();

        Assert.That(session.PendingPoints, Is.Zero);
        Assert.That(session.KillCount, Is.Zero);
        Assert.That(session.ElapsedTime, Is.Zero);
        Assert.That(session.OvertimeTier, Is.Zero);
        Assert.That(session.DifficultyId, Is.Empty);
        Assert.That(session.RandomSeed, Is.Zero);
    }

    [Test]
    public void AdvanceTime_IgnoresNonFiniteInput()
    {
        var session = new GameSession();

        session.AdvanceTime(float.NaN, 300f);
        session.AdvanceTime(float.PositiveInfinity, 300f);

        Assert.That(session.ElapsedTime, Is.Zero);
        Assert.That(session.OvertimeTier, Is.Zero);
    }
}
