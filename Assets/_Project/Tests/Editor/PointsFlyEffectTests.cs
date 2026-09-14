using NUnit.Framework;
using UnityEngine;
using _Project.UI.Common;

namespace _Project.Tests.Editor
{
    public sealed class PointsFlyEffectTests
    {
        [Test]
        public void CalculatePosition_AtBounds_ReturnsEndpoints()
        {
            Vector3 start = new Vector3(1f, 2f, 3f);
            Vector3 target = new Vector3(7f, 8f, 9f);

            Assert.That(PointsFlyEffectMath.CalculatePosition(start, target, 0f, 2f), Is.EqualTo(start));
            Assert.That(PointsFlyEffectMath.CalculatePosition(start, target, 1f, 2f), Is.EqualTo(target));
        }

        [Test]
        public void CalculatePosition_AtMiddle_AddsArcHeight()
        {
            Vector3 position = PointsFlyEffectMath.CalculatePosition(
                Vector3.zero,
                Vector3.forward * 10f,
                0.5f,
                4f);

            Assert.That(position.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(position.y, Is.EqualTo(4f).Within(0.001f));
            Assert.That(position.z, Is.EqualTo(5f).Within(0.001f));
        }
    }
}
