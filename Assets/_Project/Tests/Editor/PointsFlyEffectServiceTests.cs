using NUnit.Framework;
using Services;
using UnityEngine;

namespace _Project.Tests.Editor
{
    public sealed class PointsFlyEffectServiceTests
    {
        [Test]
        public void Publish_NotifiesSubscribersWithPointsAndPosition()
        {
            var service = new PointsFlyEffectService();
            int receivedPoints = 0;
            Vector3 receivedPosition = default;
            service.PointsAwarded += (points, position) =>
            {
                receivedPoints = points;
                receivedPosition = position;
            };

            Vector3 expectedPosition = new Vector3(2f, 3f, 4f);
            service.Publish(25, expectedPosition);

            Assert.That(receivedPoints, Is.EqualTo(25));
            Assert.That(receivedPosition, Is.EqualTo(expectedPosition));
        }
    }
}
