using System.Collections.Generic;
using CombatSystem;
using Interfaces;
using NUnit.Framework;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>验证共享动作资产收到的每次施法身份独立且不会被后续施法覆盖。</summary>
    public sealed class SkillContextBehaviorTests
    {
        /// <summary>同一动作资产被不同技能和来源调用时，各上下文保留自己的定义与身份。</summary>
        [Test]
        public void ExecutionsKeepTheirOwnContext()
        {
            var first = SkillDefinitionSO.CreateRuntimeDefinition("first", 2f);
            var second = SkillDefinitionSO.CreateRuntimeDefinition("second", 3f);
            var action = ScriptableObject.CreateInstance<RecordingSkillActionSO>();
            try
            {
                AbilityCardTestData.SetAction(first, action);
                AbilityCardTestData.SetAction(second, action);
                var executor = new SkillExecutor(null, new AttackInstanceIdGenerator());
                var targets = new[] { new Target() };

                Assert.That(executor.Execute(first, 11, Vector3.right, targets).Succeeded, Is.True);
                Assert.That(executor.Execute(second, 22, Vector3.left, targets).Succeeded, Is.True);
                Assert.That(action.Contexts, Has.Count.EqualTo(2));
                Assert.That(action.Contexts[0].Definition, Is.SameAs(first));
                Assert.That(action.Contexts[0].SourceInstanceId, Is.EqualTo(11));
                Assert.That(action.Contexts[0].SourcePosition, Is.EqualTo(Vector3.right));
                Assert.That(action.Contexts[0].AttackInstanceId, Is.EqualTo(1));
                Assert.That(action.Contexts[1].Definition, Is.SameAs(second));
                Assert.That(action.Contexts[1].SourceInstanceId, Is.EqualTo(22));
                Assert.That(action.Contexts[1].SourcePosition, Is.EqualTo(Vector3.left));
                Assert.That(action.Contexts[1].AttackInstanceId, Is.EqualTo(2));
            }
            finally
            {
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(action);
            }
        }

        private sealed class Target : IDamageable
        {
            public Vector3 Position => Vector3.zero;
            public bool IsAlive => true;
            public Transform Transform => null;
        }
    }

    /// <summary>记录动作公开输入，避免通过反射读取执行器内部状态。</summary>
    public sealed class RecordingSkillActionSO : SkillActionDefinitionSO
    {
        public readonly List<SkillExecutionContext> Contexts = new();

        /// <summary>接收本次上下文，不产生战斗副作用。</summary>
        public override bool TryExecute(in SkillExecutionContext context,
            IReadOnlyList<IDamageable> targets, IDamageRequestProcessor damageProcessor, out int appliedTargetCount)
        {
            Contexts.Add(context);
            appliedTargetCount = 0;
            return true;
        }
    }
}
