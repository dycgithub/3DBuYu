using System;
using System.Reflection;
using GameSystem;
using NUnit.Framework;
using Services;

public class RunEnergyAndSettlementTests
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
    public void Tick_UsesTheBaseEnergyDrainRate()
    {
        _energy.Initialize(100f, 100f);

        float drained = _energy.Tick(1f, 3f);

        Assert.That(drained, Is.EqualTo(3f));
        Assert.That(_energy.CurrentEnergy.CurrentValue, Is.EqualTo(97f));
    }

    [Test]
    public void AddEnergy_ReturnsOnlyTheAmountThatFitsWithinTheCap()
    {
        _energy.Initialize(5f, 10f);

        float gained = _energy.AddEnergy(20f);

        Assert.That(gained, Is.EqualTo(5f));
        Assert.That(_energy.CurrentEnergy.CurrentValue, Is.EqualTo(10f));
    }

    [Test]
    public void EnergyDepleted_EmitsOnceUntilEnergyIsRestored()
    {
        _energy.Initialize(1f, 1f);
        int eventCount = 0;
        _energy.EnergyDepleted += () => eventCount++;

        _energy.Drain(1f, EnergySpendKind.TimeFlow);
        _energy.Drain(1f, EnergySpendKind.Shot);
        _energy.AddEnergy(1f);
        _energy.Drain(1f, EnergySpendKind.Skill);

        Assert.That(eventCount, Is.EqualTo(2));
    }

    [TestCase(EnergySpendKind.EquipmentUpkeep)]
    [TestCase(EnergySpendKind.MenuFusion)]
    public void EveryEnergySpendKind_UsesTheRequestedBaseAmount(EnergySpendKind kind)
    {
        _energy.Initialize(100f, 100f);

        float drained = _energy.Drain(3f, kind);

        Assert.That(drained, Is.EqualTo(3f));
    }

    [TestCase(299.99f, 300f, 10, 10, false)]
    [TestCase(300f, 300f, 9, 10, false)]
    [TestCase(300f, 300f, 10, 10, true)]
    [TestCase(300f, 300f, 0, 0, true)]
    public void MeetsRunRequirements_UsesDurationAndKillConditions(
        float elapsedTime,
        float targetDuration,
        int killCount,
        int targetKillCount,
        bool expected)
    {
        bool result = RunRuleMath.MeetsRunRequirements(
            elapsedTime,
            targetDuration,
            killCount,
            targetKillCount);

        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void CalculateSettlementReward_UsesTheDifficultyMultiplier()
    {
        Assert.That(CalculateSettlementReward(100, 1.5f), Is.EqualTo(150));
    }

    [Test]
    public void CalculateSettlementReward_InvalidMultipliersReturnZero()
    {
        Assert.That(CalculateSettlementReward(100, 0f), Is.Zero);
        Assert.That(CalculateSettlementReward(100, -1f), Is.Zero);
        Assert.That(CalculateSettlementReward(100, float.NaN), Is.Zero);
        Assert.That(CalculateSettlementReward(100, float.PositiveInfinity), Is.Zero);
    }

    [Test]
    public void CalculateSettlementReward_ClampsAtIntMaximum()
    {
        Assert.That(
            CalculateSettlementReward(int.MaxValue, 20f),
            Is.EqualTo(int.MaxValue));
    }

    private static int CalculateSettlementReward(
        int sessionPoints,
        float settlementMultiplier)
    {
        MethodInfo method = typeof(RunRuleMath).GetMethod(
            "CalculateSettlementReward",
            BindingFlags.Static | BindingFlags.Public,
            null,
            new[] { typeof(int), typeof(float) },
            null);

        Assert.That(method, Is.Not.Null);
        return method == null
            ? 0
            : (int)method.Invoke(
                null,
                new object[] { sessionPoints, settlementMultiplier });
    }
}
