using System.Collections.Generic;
using CombatSystem;
using Interfaces;
using NUnit.Framework;
using UnityEngine;

namespace CombatSystem.Tests
{
    /// <summary>
    /// 验证技能和投射物使用同一攻击编号来源，便于命中追踪与去重。
    /// </summary>
    public sealed class SkillAttackInstanceIdContractTests
    {
        private readonly List<Object> _temporaryObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int index = 0; index < _temporaryObjects.Count; index++)
            {
                if (_temporaryObjects[index] != null)
                {
                    Object.DestroyImmediate(_temporaryObjects[index]);
                }
            }

            _temporaryObjects.Clear();
        }

        [Test]
        public void SkillAndProjectileShareAttackInstanceIdGenerator()
        {
            SkillDefinitionSO definition = Track(
                SkillDefinitionSO.CreateRuntimeDefinition("shared-attack-id", 10f));
            var target = new TestDamageable();
            var damageProcessor = new RecordingDamageProcessor();
            var ids = new AttackInstanceIdGenerator();
            var executor = new SkillExecutor(damageProcessor, ids);
            var spawner = new RecordingProjectileSpawner();
            var launcher = new ProjectileLauncher(
                spawner,
                ids);
            ProjectileProfileSO profile = Track(
                ProjectileProfileSO.CreateRuntimeProfile("shared-attack-id-projectile"));

            SkillExecutionResult skillResult = executor.Execute(
                definition,
                1,
                Vector3.zero,
                new[] { target });
            launcher.Launch(new ProjectileLaunchRequest(
                1,
                profile,
                Vector3.zero,
                Vector3.forward,
                1f));

            Assert.That(skillResult.Succeeded, Is.True);
            Assert.That(damageProcessor.LastRequest.AttackInstanceId, Is.EqualTo(1));
            Assert.That(spawner.LastData.AttackInstanceId, Is.EqualTo(2));
        }

        private T Track<T>(T temporaryObject) where T : Object
        {
            _temporaryObjects.Add(temporaryObject);
            return temporaryObject;
        }

        private sealed class RecordingDamageProcessor : IDamageRequestProcessor
        {
            public DamageRequest LastRequest { get; private set; }

            public bool TryApplyDamage(
                IDamageable target,
                in DamageRequest request,
                out DamageResult result)
            {
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
            public ProjectileData LastData { get; private set; }

            public bool TrySpawnProjectile(in ProjectileData projectileData)
            {
                LastData = projectileData;
                return true;
            }

            public bool TrySpawnProjectileBatch(
                in ProjectileData projectileData,
                int projectileCount)
            {
                LastData = projectileData;
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
