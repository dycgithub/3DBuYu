using System.Collections.Generic;
using Interfaces;
using NUnit.Framework;
using UnityEngine;

namespace CombatSystem.Tests
{
    /// <summary>
    /// 验证唯一技能执行器和投射物入口接收显式目标且不依赖玩家资源状态。
    /// </summary>
    public sealed class UniversalCombatExecutionTests
    {
        private readonly List<Object> _temporaryObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int index = 0; index < _temporaryObjects.Count; index++)
            {
                Object temporaryObject = _temporaryObjects[index];
                if (temporaryObject != null)
                {
                    Object.DestroyImmediate(temporaryObject);
                }
            }

            _temporaryObjects.Clear();
        }

        [Test]
        public void SkillExecutionService_ExecutesWithoutPlayerPrerequisites()
        {
            SkillDefinitionSO definition = Track(
                SkillDefinitionSO.CreateRuntimeDefinition("enemy-skill", 15f));
            var target = new TestDamageable();
            var damageProcessor = new RecordingDamageProcessor();
            var service = new SkillExecutor(damageProcessor, new AttackInstanceIdGenerator());
            var request = new SkillExecutionRequest(
                definition,
                sourceInstanceId: 77,
                sourcePosition: new Vector3(1f, 2f, 3f),
                targets: new[] { target });

            SkillExecutionResult result = service.Execute(in request);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.AppliedTargetCount, Is.EqualTo(1));
            Assert.That(damageProcessor.LastRequest.SourceInstanceId, Is.EqualTo(77));
            Assert.That(damageProcessor.LastRequest.DamageAmount, Is.EqualTo(15f));
            Assert.That(damageProcessor.Target, Is.SameAs(target));
        }

        [Test]
        public void ProjectileLauncher_LaunchesWithoutEnergyOrCooldown()
        {
            ProjectileProfileSO profile = Track(
                ProjectileProfileSO.CreateRuntimeProfile("shared-projectile"));
            profile.EnergyCost = 999f;
            profile.SpeedUnitsPerSecond = 20f;
            profile.MaximumTravelDistance = 80f;
            var spawner = new RecordingProjectileSpawner();
            var launcher = new ProjectileLauncher(spawner);
            var request = new ProjectileLaunchRequest(
                sourceInstanceId: 42,
                projectileProfile: profile,
                originPosition: Vector3.zero,
                launchDirection: Vector3.forward,
                damageAmount: 25f,
                projectileCount: 3,
                penetrationCount: 1);

            bool launched = launcher.Launch(in request);

            Assert.That(launched, Is.True);
            Assert.That(spawner.BatchCallCount, Is.EqualTo(1));
            Assert.That(spawner.LastProjectileCount, Is.EqualTo(3));
            Assert.That(spawner.LastData.SourceInstanceId, Is.EqualTo(42));
            Assert.That(spawner.LastData.DamageAmount, Is.EqualTo(25f));
            Assert.That(spawner.LastData.PenetrationCount, Is.EqualTo(1));
            Assert.That(spawner.LastData.ProjectileProfile, Is.SameAs(profile));
            Assert.That(spawner.LastAttackEnergyCost, Is.Zero);
        }

        [Test]
        public void SkillExecutionService_UsesExplicitTargetsInsteadOfEnemyLayer()
        {
            SkillDefinitionSO definition = Track(
                SkillDefinitionSO.CreateRuntimeDefinition("explicit-target", 8f));
            var expectedTarget = new TestDamageable();
            var damageProcessor = new RecordingDamageProcessor();
            var service = new SkillExecutor(damageProcessor, new AttackInstanceIdGenerator());
            var request = new SkillExecutionRequest(
                definition,
                sourceInstanceId: 101,
                sourcePosition: Vector3.zero,
                targets: new[] { expectedTarget });

            SkillExecutionResult result = service.Execute(in request);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(damageProcessor.Target, Is.SameAs(expectedTarget));
        }

        private T Track<T>(T temporaryObject) where T : Object
        {
            _temporaryObjects.Add(temporaryObject);
            return temporaryObject;
        }

        private sealed class RecordingDamageProcessor : IDamageRequestProcessor
        {
            public IDamageable Target { get; private set; }
            public DamageRequest LastRequest { get; private set; }

            public bool TryApplyDamage(
                IDamageable target,
                in DamageRequest request,
                out DamageResult result)
            {
                Target = target;
                LastRequest = request;
                result = new DamageResult
                {
                    Outcome = DamageOutcome.Applied,
                    AppliedDamage = request.DamageAmount
                };
                return true;
            }
        }

        private sealed class RecordingProjectileSpawner : IProjectileSpawner
        {
            public int BatchCallCount { get; private set; }
            public int LastProjectileCount { get; private set; }
            public ProjectileData LastData { get; private set; }
            public float LastAttackEnergyCost { get; private set; }

            public bool TrySpawnProjectile(in ProjectileData projectileData)
            {
                LastData = projectileData;
                LastProjectileCount = 1;
                return true;
            }

            public bool TrySpawnProjectileBatch(
                in ProjectileData projectileData,
                int projectileCount)
            {
                BatchCallCount++;
                LastData = projectileData;
                LastProjectileCount = projectileCount;
                LastAttackEnergyCost = 0f;
                return true;
            }
        }

        private sealed class TestDamageable : IDamageable
        {
            public Vector3 Position => Vector3.zero;
            public bool IsAlive => true;
            public Transform Transform => null;
        }
    }
}
