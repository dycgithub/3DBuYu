using GameSystem;
using NUnit.Framework;

/// <summary>
/// 验证单局胜负判定和胜利结算奖励的纯数学规则。
/// </summary>
public sealed class RunRuleMathTests
{
    [TestCase(299.99f, 300f, 0, 0, false)]
    [TestCase(300f, 300f, 0, 0, true)]
    [TestCase(300f, 300f, -1, 0, true)]
    [TestCase(300f, 300f, 4, 5, false)]
    [TestCase(300f, 300f, 5, 5, true)]
    [TestCase(300f, 300f, 5, 0, true)]
    public void MeetsRunRequirements_ReturnsExpectedVerdict(
        float elapsedTime,
        float targetDuration,
        int killCount,
        int targetKillCount,
        bool expected)
    {
        bool actual = RunRuleMath.MeetsRunRequirements(
            elapsedTime,
            targetDuration,
            killCount,
            targetKillCount);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void MeetsRunRequirements_RejectsNonFiniteElapsedTime()
    {
        Assert.That(
            RunRuleMath.MeetsRunRequirements(float.NaN, 300f, 0, 0),
            Is.False);
        Assert.That(
            RunRuleMath.MeetsRunRequirements(float.PositiveInfinity, 300f, 0, 0),
            Is.False);
    }

    [TestCase(37, 2.5f, 92)]
    [TestCase(100, 1f, 100)]
    [TestCase(1, 0.5f, 0)]
    [TestCase(0, 2f, 0)]
    [TestCase(-1, 2f, 0)]
    [TestCase(100, 0f, 0)]
    [TestCase(100, -1f, 0)]
    public void CalculateSettlementReward_ReturnsFlooredReward(
        int sessionPoints,
        float settlementMultiplier,
        int expected)
    {
        int actual = RunRuleMath.CalculateSettlementReward(
            sessionPoints,
            settlementMultiplier);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void CalculateSettlementReward_RejectsNonFiniteMultiplier()
    {
        Assert.That(
            RunRuleMath.CalculateSettlementReward(100, float.NaN),
            Is.Zero);
        Assert.That(
            RunRuleMath.CalculateSettlementReward(100, float.PositiveInfinity),
            Is.Zero);
    }

    [Test]
    public void CalculateSettlementReward_ClampsOverflow()
    {
        int actual = RunRuleMath.CalculateSettlementReward(int.MaxValue, 2f);

        Assert.That(actual, Is.EqualTo(int.MaxValue));
    }
}
