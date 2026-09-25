using GameSystem;
using NUnit.Framework;
using Services;

/// <summary>
/// 验证超时阶段、分段积分和统一能量消耗修正。
/// </summary>
public sealed class OvertimeMechanicTests
{
    [TestCase(0f, 2f)]
    [TestCase(4.99f, 2f)]
    [TestCase(5f, 3f)]
    [TestCase(10f, 4f)]
    [TestCase(15f, 5f)]
    [TestCase(20f, 6f)]
    public void CalculateOvertimeMultiplier_IncreasesEveryFiveSeconds(
        float overtimeElapsedTime,
        float expectedMultiplier)
    {
        float actual = RunRuleMath.CalculateOvertimeMultiplier(overtimeElapsedTime);

        Assert.That(actual, Is.EqualTo(expectedMultiplier));
    }

    [Test]
    public void TargetDurationWithoutEnoughKills_FailsImmediately()
    {
        RunRuleService rules = new();

        Assert.That(
            rules.ShouldFailAtTarget(300f, 300f, 4, 5),
            Is.True);
        Assert.That(
            rules.ShouldFailAtTarget(299.99f, 300f, 4, 5),
            Is.False);
        Assert.That(
            rules.ShouldFailAtTarget(300f, 300f, 5, 5),
            Is.False);
    }

    [Test]
    public void SettlementReward_SeparatesNormalAndOvertimePoints()
    {
        int actual = RunRuleMath.CalculateSettlementReward(
            basePoints: 100,
            overtimePoints: 50,
            difficultyMultiplier: 2f,
            overtimeMultiplier: 3f);

        Assert.That(actual, Is.EqualTo(350));
    }

    [Test]
    public void GameSession_TracksOvertimePointsSeparately()
    {
        GameSession session = new();
        session.BeginRun("easy", 0);
        session.RecordKill(100);
        session.EnterOvertime(0f);
        session.RecordKill(50);

        Assert.That(session.BasePoints, Is.EqualTo(100));
        Assert.That(session.OvertimePoints, Is.EqualTo(50));
        Assert.That(session.SessionPoints, Is.EqualTo(150));
        Assert.That(session.IsOvertime, Is.True);
    }

    [Test]
    public void EnergyService_AppliesMultiplierToAllConsumptionButNotRewards()
    {
        EnergyService energy = new();
        energy.Initialize(100f, 100f);
        energy.SetConsumptionMultiplier(3f);

        Assert.That(energy.GetEffectiveCost(2f), Is.EqualTo(6f));
        Assert.That(energy.TrySpend(2f, EnergySpendKind.Shot), Is.True);
        Assert.That(energy.CurrentEnergy.CurrentValue, Is.EqualTo(94f));

        energy.Tick(2f, 1f);
        Assert.That(energy.CurrentEnergy.CurrentValue, Is.EqualTo(88f));

        energy.AddEnergy(2f);
        Assert.That(energy.CurrentEnergy.CurrentValue, Is.EqualTo(90f));

        energy.AddEnergy(energy.GetEffectiveCost(2f));
        Assert.That(energy.CurrentEnergy.CurrentValue, Is.EqualTo(96f));
        energy.Dispose();
    }
}
