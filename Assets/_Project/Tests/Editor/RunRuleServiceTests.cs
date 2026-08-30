using GameSystem;
using NUnit.Framework;

public class RunRuleServiceTests
{
    private readonly RunRuleService _rules = new();

    [Test]
    public void JudgeEnergyDepleted_BeforeTargetDuration_ReturnsDefeat()
    {
        RunVerdict verdict = _rules.JudgeEnergyDepleted(299.99f, 300f, 10, 10);

        Assert.That(verdict, Is.EqualTo(RunVerdict.Defeat));
    }

    [Test]
    public void JudgeEnergyDepleted_AtTargetDurationWithMissingKills_ReturnsDefeat()
    {
        RunVerdict verdict = _rules.JudgeEnergyDepleted(300f, 300f, 9, 10);

        Assert.That(verdict, Is.EqualTo(RunVerdict.Defeat));
    }

    [Test]
    public void JudgeEnergyDepleted_AtTargetDurationWithRequiredKills_ReturnsVictory()
    {
        RunVerdict verdict = _rules.JudgeEnergyDepleted(300f, 300f, 10, 10);

        Assert.That(verdict, Is.EqualTo(RunVerdict.Victory));
    }

    [Test]
    public void JudgeEnergyDepleted_WithNoKillTarget_ReturnsVictoryAtTargetDuration()
    {
        RunVerdict verdict = _rules.JudgeEnergyDepleted(300f, 300f, 0, 0);

        Assert.That(verdict, Is.EqualTo(RunVerdict.Victory));
    }
}
