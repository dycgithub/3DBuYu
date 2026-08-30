using System;
using System.Collections.Generic;
using GameSystem;
using NUnit.Framework;
using R3;

public sealed class KillStreakServiceTests
{
    private KillStreakService _service;

    [SetUp]
    public void SetUp()
    {
        _service = new KillStreakService();
    }

    [TearDown]
    public void TearDown()
    {
        _service.Dispose();
    }

    [Test]
    public void RegisterKill_IncrementsCurrentAndBestStreak()
    {
        _service.BeginRun(3f);

        Assert.That(_service.RegisterKill(), Is.EqualTo(1));
        Assert.That(_service.RegisterKill(), Is.EqualTo(2));
        Assert.That(_service.CurrentStreak.CurrentValue, Is.EqualTo(2));
        Assert.That(_service.BestStreak.CurrentValue, Is.EqualTo(2));
    }

    [Test]
    public void Tick_ResetsCurrentAfterWindowAndKeepsBest()
    {
        _service.BeginRun(3f);
        _service.RegisterKill();
        _service.RegisterKill();

        _service.Tick(2f);
        Assert.That(_service.CurrentStreak.CurrentValue, Is.EqualTo(2));

        _service.Tick(1f);
        Assert.That(_service.CurrentStreak.CurrentValue, Is.Zero);
        Assert.That(_service.BestStreak.CurrentValue, Is.EqualTo(2));
    }

    [Test]
    public void BeginRun_ClearsPreviousRunValues()
    {
        _service.BeginRun(3f);
        _service.RegisterKill();
        _service.RegisterKill();

        _service.BeginRun(5f);

        Assert.That(_service.CurrentStreak.CurrentValue, Is.Zero);
        Assert.That(_service.BestStreak.CurrentValue, Is.Zero);
        Assert.That(_service.RegisterKill(), Is.EqualTo(1));
    }

    [Test]
    public void RegisterKill_BeforeBeginRunIsIgnored()
    {
        Assert.That(_service.RegisterKill(), Is.Zero);
        Assert.That(_service.CurrentStreak.CurrentValue, Is.Zero);
        Assert.That(_service.BestStreak.CurrentValue, Is.Zero);
    }

    [Test]
    public void Tick_IgnoresInvalidDeltaTime()
    {
        _service.BeginRun(3f);
        _service.RegisterKill();

        _service.Tick(-1f);
        _service.Tick(float.NaN);
        _service.Tick(float.PositiveInfinity);

        Assert.That(_service.CurrentStreak.CurrentValue, Is.EqualTo(1));
    }

    [Test]
    public void ReactiveProperties_PublishInitialAndUpdatedValues()
    {
        _service.BeginRun(3f);
        var currentValues = new List<int>();
        IDisposable subscription = _service.CurrentStreak.Subscribe(currentValues.Add);

        _service.RegisterKill();
        _service.Tick(3f);
        subscription.Dispose();

        Assert.That(currentValues, Is.EqualTo(new[] { 0, 1, 0 }));
    }
}
