using FlockingSystem.ECS;
using NUnit.Framework;
using Unity.Mathematics;

namespace FlockingSystem.Tests
{
    /// <summary>
    /// 锁定群游数学和配置归一化契约，防止删减重复代码时改变模拟输入输出。
    /// </summary>
    public sealed class EnemyFlockRefactorTests
    {
        [Test]
        public void ProfileData_ClampsDistancesSpeedAndMultiplier()
        {
            EnemyFlockProfile profile = new(
                visualIndex: 7,
                neighbourDistance: 0f,
                separationDistance: -1f,
                rotationSpeed: -1f,
                minSpeed: 2f,
                maxSpeed: 1f,
                speedMultiplier: -3f);

            EnemyFlockAgent agent = profile.CreateAgentData(1f);

            Assert.That(agent.NeighbourDistance, Is.EqualTo(0.01f).Within(0.0001f));
            Assert.That(agent.SeparationDistance, Is.EqualTo(0.01f).Within(0.0001f));
            Assert.That(agent.RotationSpeed, Is.Zero);
            Assert.That(agent.MinSpeed, Is.EqualTo(2f));
            Assert.That(agent.MaxSpeed, Is.EqualTo(2f));
            Assert.That(agent.Speed, Is.EqualTo(2f));
            Assert.That(agent.SpeedMultiplier, Is.Zero);
        }

        [Test]
        public void MovementDelta_CombinesVelocityAndImpulseUsingClampedStep()
        {
            EnemyFlockAgent agent = new()
            {
                Velocity = new float3(2f, 0f, 0f),
                SpeedMultiplier = 2f,
                ExternalImpulse = new float3(0f, 1f, 0f),
            };

            float3 delta = EnemyFlockBoidsMath.CalculateMovementDelta(agent, 0.5f);
            float3 negativeStep = EnemyFlockBoidsMath.CalculateMovementDelta(agent, -1f);

            Assert.That(delta.x, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(delta.y, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(math.lengthsq(negativeStep), Is.EqualTo(0f));
        }

        [Test]
        public void OverlappingAgents_ReceiveOppositeStableSeparationDirections()
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

            Assert.That(math.lengthsq(first), Is.GreaterThan(0f));
            Assert.That(math.length(first + second), Is.LessThan(0.0001f));
        }

        [Test]
        public void BoundaryForce_OutsideBoundsPointsBackIntoSwimmingArea()
        {
            EnemyFlockWorldConfig config = new()
            {
                SwimCenter = float3.zero,
                SwimLimits = new float3(10f, 10f, 10f),
                BoundaryMargin = 2f,
            };

            float3 force = EnemyFlockBoidsMath.CalculateBoundaryForce(
                new float3(12f, 0f, 0f),
                config);

            Assert.That(force.x, Is.LessThan(0f));
        }

        [Test]
        public void ChunkSeed_IsDeterministicAndNeverZero()
        {
            uint first = EnemyFlockBoidsMath.GetChunkSeed(123u, 4);
            uint repeat = EnemyFlockBoidsMath.GetChunkSeed(123u, 4);
            uint other = EnemyFlockBoidsMath.GetChunkSeed(123u, 5);

            Assert.That(first, Is.Not.EqualTo(0u));
            Assert.That(first, Is.EqualTo(repeat));
            Assert.That(first, Is.Not.EqualTo(other));
        }
    }
}
