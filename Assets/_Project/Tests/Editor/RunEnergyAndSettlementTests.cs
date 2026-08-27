using GameSystem;
using NUnit.Framework;
using Services;

public class RunEnergyAndSettlementTests
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
    public void Tick_UsesTheCurrentOvertimeCostMultiplier()
    {
        _energy.Initialize(100f, 100f);
        _energy.SetCostMultiplier(2f);

        float drained = _energy.Tick(1f, 3f);

        Assert.That(drained, Is.EqualTo(6f));
        Assert.That(_energy.CurrentEnergy.CurrentValue, Is.EqualTo(94f));
    }

    [Test]
    public void AddEnergy_DoesNotApplyTheCostMultiplier()
    {
        _energy.Initialize(5f, 10f);
        _energy.SetCostMultiplier(3f);

        float gained = _energy.AddEnergy(1f);

        Assert.That(gained, Is.EqualTo(1f));
        Assert.That(_energy.CurrentEnergy.CurrentValue, Is.EqualTo(6f));
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
    public void CalculateScaledPoints_UsesFloorAfterApplyingMultiplier()
    {
        Assert.That(RunRuleMath.CalculateScaledPoints(25, 2.4f), Is.EqualTo(60));
        Assert.That(RunRuleMath.CalculateScaledPoints(25, 1.99f), Is.EqualTo(49));
    }

    [Test]
    public void CalculateSettlementReward_UsesTheDifficultyMultiplier()
    {
        Assert.That(RunRuleMath.CalculateSettlementReward(101, 1.5f), Is.EqualTo(151));
    }
}
