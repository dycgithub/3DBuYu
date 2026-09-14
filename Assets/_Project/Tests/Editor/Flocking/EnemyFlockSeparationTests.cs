using FlockingSystem.ECS;
using NUnit.Framework;
using Unity.Mathematics;

namespace FlockingSystem.Tests
{
    public sealed class EnemyFlockSeparationTests
    {
        [Test]
        public void SeparationContribution_ProvidesStableDirectionWhenAgentsOverlap()
        {
            float3 result = EnemyFlockBoidsMath.CalculateSeparationContribution(
                float3.zero,
                float3.zero,
                2f,
                3,
                8);

            Assert.That(math.lengthsq(result), Is.GreaterThan(0f));
        }

        [Test]
        public void SeparationContribution_IsOppositeForBothAgentsWhenTheyOverlap()
        {
            float3 first = EnemyFlockBoidsMath.CalculateSeparationContribution(
                float3.zero,
                float3.zero,
                2f,
                3,
                8);
            float3 second = EnemyFlockBoidsMath.CalculateSeparationContribution(
                float3.zero,
                float3.zero,
                2f,
                8,
                3);

            Assert.That(math.length(first + second), Is.LessThan(0.0001f));
        }

        [Test]
        public void SeparationForce_PreservesStrongerContributionForCloserAgent()
        {
            float3 close = EnemyFlockBoidsMath.CalculateSeparationForce(
                EnemyFlockBoidsMath.CalculateSeparationContribution(
                    float3.zero,
                    new float3(0.5f, 0f, 0f),
                    2f,
                    0,
                    1),
                1f);
            float3 far = EnemyFlockBoidsMath.CalculateSeparationForce(
                EnemyFlockBoidsMath.CalculateSeparationContribution(
                    float3.zero,
                    new float3(1.5f, 0f, 0f),
                    2f,
                    0,
                    1),
                1f);

            Assert.That(math.length(close), Is.GreaterThan(math.length(far)));
        }
    }
}
