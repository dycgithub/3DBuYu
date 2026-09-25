using NUnit.Framework;
using UnityEngine;
using _Project.UI.Damage;

public sealed class DamageNumberMathTests
{
    [Test]
    public void NormalDamage_AggregatesForSameTargetWithinWindow()
    {
        bool canAggregate = DamageNumberMath.CanAggregate(
            12,
            12,
            DamageNumberPriority.Normal,
            DamageNumberPriority.Normal,
            0.05f,
            0.12f);

        Assert.IsTrue(canAggregate);
    }

    [Test]
    public void CriticalDamage_NeverAggregatesWithNormalDamage()
    {
        bool canAggregate = DamageNumberMath.CanAggregate(
            12,
            12,
            DamageNumberPriority.Normal,
            DamageNumberPriority.Critical,
            0.05f,
            0.12f);

        Assert.IsFalse(canAggregate);
    }

    [Test]
    public void Aggregation_RejectsDifferentTargetAndExpiredWindow()
    {
        Assert.IsFalse(DamageNumberMath.CanAggregate(
            12,
            13,
            DamageNumberPriority.Normal,
            DamageNumberPriority.Normal,
            0.05f,
            0.12f));
        Assert.IsFalse(DamageNumberMath.CanAggregate(
            12,
            12,
            DamageNumberPriority.Normal,
            DamageNumberPriority.Normal,
            0.13f,
            0.12f));
    }

    [Test]
    public void CriticalPriority_ReplacesNormalPriority()
    {
        Assert.IsTrue(DamageNumberMath.HasHigherPriority(
            DamageNumberPriority.Critical,
            DamageNumberPriority.Normal));
        Assert.IsFalse(DamageNumberMath.HasHigherPriority(
            DamageNumberPriority.Normal,
            DamageNumberPriority.Critical));
    }

    [Test]
    public void Offset_IsDeterministicAndBounded()
    {
        Vector2 first = DamageNumberMath.GetOffset(12, 4);
        Vector2 second = DamageNumberMath.GetOffset(12, 4);

        Assert.AreEqual(first, second);
        Assert.LessOrEqual(Mathf.Abs(first.x), 1f);
        Assert.LessOrEqual(Mathf.Abs(first.y), 1f);
    }
}
