using NUnit.Framework;
using _Project.UI.MetaballMenu;

public sealed class MetaballFusionProgressTests
{
    [Test]
    public void PaidHeldFrames_AdvanceUntilCompletion()
    {
        var progress = new MetaballFusionProgress(1f);

        Assert.That(progress.Advance(0.25f, true, true), Is.False);
        Assert.That(progress.Value, Is.EqualTo(0.25f).Within(0.0001f));
        Assert.That(progress.Advance(0.75f, true, true), Is.True);
        Assert.That(progress.Value, Is.EqualTo(1f).Within(0.0001f));
    }

    [Test]
    public void UnpaidHeldFrame_DoesNotAdvance()
    {
        var progress = new MetaballFusionProgress(1f);

        Assert.That(progress.Advance(0.5f, true, false), Is.False);
        Assert.That(progress.Value, Is.Zero);
    }

    [Test]
    public void ReleasedBeforeCompletion_ResetsProgressWithoutChangingPaidState()
    {
        var progress = new MetaballFusionProgress(1f);
        progress.Advance(0.4f, true, true);

        Assert.That(progress.Advance(0f, false, false), Is.False);
        Assert.That(progress.Value, Is.Zero);
        Assert.That(progress.RequiresRelease, Is.False);
    }

    [Test]
    public void CompletedProgress_RequiresReleaseBeforeStartingAgain()
    {
        var progress = new MetaballFusionProgress(1f);

        Assert.That(progress.Advance(1f, true, true), Is.True);
        Assert.That(progress.Advance(1f, true, true), Is.False);
        Assert.That(progress.Value, Is.EqualTo(1f).Within(0.0001f));

        progress.Advance(0f, false, false);
        Assert.That(progress.RequiresRelease, Is.False);
        Assert.That(progress.Advance(0.5f, true, true), Is.False);
        Assert.That(progress.Value, Is.EqualTo(0.5f).Within(0.0001f));
    }

    [Test]
    public void RequireRelease_ClearsProgressUntilKeyIsReleased()
    {
        var progress = new MetaballFusionProgress(1f);
        progress.Advance(0.5f, true, true);

        progress.RequireRelease();

        Assert.That(progress.Value, Is.Zero);
        Assert.That(progress.Advance(0.5f, true, true), Is.False);
        Assert.That(progress.Value, Is.Zero);
        progress.Advance(0f, false, false);
        Assert.That(progress.RequiresRelease, Is.False);
    }
}
