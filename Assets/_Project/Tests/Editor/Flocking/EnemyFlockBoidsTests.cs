using FlockingSystem.ECS;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace FlockingSystem.Tests
{
    /// <summary>
    /// 验证 ECS Flocking 的核心数学规则、速度积分、Chunk 容量和 Chunk 随机身份。
    /// </summary>
    public sealed class EnemyFlockBoidsTests
    {
        [Test]
        public void SeparationContribution_PointsAwayFromNearbyAgent()
        {
            float3 result = EnemyFlockBoidsMath.CalculateSeparationContribution(
                float3.zero,
                new float3(1f, 0f, 0f),
                2f);

            Assert.That(result.x, Is.LessThan(0f));
            Assert.That(math.lengthsq(result), Is.GreaterThan(0f));
        }

        [Test]
        public void SeparationContribution_IgnoresAgentOutsideSeparationRadius()
        {
            float3 result = EnemyFlockBoidsMath.CalculateSeparationContribution(
                float3.zero,
                new float3(3f, 0f, 0f),
                2f);

            Assert.That(math.lengthsq(result), Is.EqualTo(0f));
        }

        [Test]
        public void CohesionPointsToAverageNeighbourPosition()
        {
            float3 result = EnemyFlockBoidsMath.CalculateCohesion(
                new float3(1f, 0f, 0f),
                new float3(4f, 2f, 0f));

            Assert.That(result.x, Is.EqualTo(3f).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void AlignmentMatchesNeighbourAverageVelocityVector()
        {
            float3 result = EnemyFlockBoidsMath.CalculateAlignment(
                new float3(1f, 0f, 0f),
                new float3(0f, 2f, 0f));

            Assert.That(result.x, Is.EqualTo(-1f).Within(0.0001f));
            Assert.That(result.y, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void BoundaryForcePointsInsideSwimmingBounds()
        {
            EnemyFlockWorldConfig config = new()
            {
                SwimCenter = float3.zero,
                SwimLimits = new float3(10f, 10f, 10f),
                BoundaryMargin = 2f,
                BoundaryWeight = 1f,
            };

            float3 result = EnemyFlockBoidsMath.CalculateBoundaryForce(
                new float3(9f, 0f, 0f),
                config);

            Assert.That(result.x, Is.LessThan(0f));
        }

        [Test]
        public void IntegrateVelocity_ClampsAccelerationAndSpeed()
        {
            float3 result = EnemyFlockBoidsMath.IntegrateVelocity(
                new float3(1f, 0f, 0f),
                new float3(100f, 0f, 0f),
                1f,
                0.5f,
                3f,
                2f);

            Assert.That(math.length(result), Is.LessThanOrEqualTo(3.0001f));
            Assert.That(math.length(result), Is.GreaterThanOrEqualTo(0.4999f));
        }

        [Test]
        public void ChunkSeed_IsNonZeroAndDifferentForDifferentFirstEntities()
        {
            uint firstSeed = EnemyFlockBoidsMath.GetChunkSeed(123u, 1);
            uint secondSeed = EnemyFlockBoidsMath.GetChunkSeed(123u, 2);

            Assert.That(firstSeed, Is.Not.EqualTo(0u));
            Assert.That(secondSeed, Is.Not.EqualTo(0u));
            Assert.That(firstSeed, Is.Not.EqualTo(secondSeed));
        }

        [Test]
        public void EnemyFlockAgent_UsesNaturalChunkCapacityAboveLegacyLimit()
        {
            World world = new("EnemyFlockBoidsTests");
            EntityManager entityManager = world.EntityManager;
            EntityArchetype archetype = entityManager.CreateArchetype(typeof(EnemyFlockAgent));
            var entities = new NativeArray<Entity>(64, Allocator.Temp);

            try
            {
                entityManager.CreateEntity(archetype, entities);
                EntityQuery query = entityManager.CreateEntityQuery(
                    ComponentType.ReadOnly<EnemyFlockAgent>());
                using NativeArray<ArchetypeChunk> chunks =
                    query.ToArchetypeChunkArray(Allocator.Temp);

                Assert.That(chunks.Length, Is.GreaterThan(0));
                Assert.That(chunks[0].Capacity, Is.GreaterThan(50));
            }
            finally
            {
                entities.Dispose();
                world.Dispose();
            }
        }

        [Test]
        public void EnemyFlockSettings_DefaultMaximumAgents_Is2048()
        {
            EnemyFlockSettingsSO settings = ScriptableObject.CreateInstance<EnemyFlockSettingsSO>();

            try
            {
                Assert.That(settings.MaximumAgents, Is.EqualTo(2048));
            }
            finally
            {
                Object.DestroyImmediate(settings);
            }
        }
    }
}
