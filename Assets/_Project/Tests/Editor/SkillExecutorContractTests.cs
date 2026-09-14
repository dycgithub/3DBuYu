using System.Collections.Generic;
using CombatSystem;
using Interfaces;
using NUnit.Framework;
using UnityEngine;

namespace CombatSystem.Tests
{
    /// <summary>
    /// 验证 SkillExecutor 使用显式依赖执行技能，无需创建玩家卡牌状态。
    /// </summary>
    public sealed class SkillExecutorContractTests
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
        public void Execute_UsesExplicitTargetsWithoutPlayerState()
        {
            SkillDefinitionSO definition = Track(
                SkillDefinitionSO.CreateRuntimeDefinition("executor-skill", 12f));
            var target = new TestDamageable();
            var damageProcessor = new RecordingDamageProcessor();
            var executor = new SkillExecutor(damageProcessor, new AttackInstanceIdGenerator());

            SkillExecutionResult result = executor.Execute(
                definition,
                sourceInstanceId: 9,
                sourcePosition: Vector3.one,
                targets: new[] { target });

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.AppliedTargetCount, Is.EqualTo(1));
            Assert.That(damageProcessor.Target, Is.SameAs(target));
            Assert.That(damageProcessor.LastRequest.SourceInstanceId, Is.EqualTo(9));
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

        private sealed class TestDamageable : IDamageable
        {
            public Vector3 Position => Vector3.zero;
            public bool IsAlive => true;
            public Transform Transform => null;
        }
    }
}
