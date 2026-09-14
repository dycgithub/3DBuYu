using System.Collections.Generic;
using System.Reflection;
using CombatSystem;
using EnemySystem;
using Interfaces;
using NUnit.Framework;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证敌人战斗能力只组装来源上下文，并把技能与投射物请求交给通用执行服务。
    /// </summary>
    public sealed class EnemyCombatAbilityBehaviorTests
    {
        [Test]
        public void ExecuteSkill_ForwardsSourcePositionAndTargets()
        {
            GameObject owner = new("EnemyCombatAbilityBehavior");
            SkillDefinitionSO definition = SkillDefinitionSO.CreateRuntimeDefinition(
                "enemy_skill",
                12f);
            RecordingSkillExecutionService service = new();
            EnemyCombatAbility ability = owner.AddComponent<EnemyCombatAbility>();
            RecordingDamageable target = new();
            IDamageable[] targets = { target };
            SetField(ability, "_skillExecutionService", service);

            try
            {
                SkillExecutionResult result = ability.ExecuteSkill(definition, targets);

                Assert.That(result.Succeeded, Is.True);
                Assert.That(service.CallCount, Is.EqualTo(1));
                Assert.That(service.LastRequest.Definition, Is.SameAs(definition));
                Assert.That(service.LastRequest.SourceInstanceId, Is.EqualTo(ability.GetInstanceID()));
                Assert.That(service.LastRequest.SourcePosition, Is.EqualTo(owner.transform.position));
                Assert.That(service.LastRequest.Targets, Is.SameAs(targets));
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void ExecuteSkill_WithNullTargetForwardsEmptyTargetList()
        {
            GameObject owner = new("EnemyCombatAbilityEmptyTarget");
            SkillDefinitionSO definition = SkillDefinitionSO.CreateRuntimeDefinition(
                "enemy_skill_without_target",
                4f);
            RecordingSkillExecutionService service = new();
            EnemyCombatAbility ability = owner.AddComponent<EnemyCombatAbility>();
            SetField(ability, "_skillExecutionService", service);

            try
            {
                ability.ExecuteSkill(definition, (IDamageable)null);

                Assert.That(service.CallCount, Is.EqualTo(1));
                Assert.That(service.LastRequest.Targets, Is.Not.Null);
                Assert.That(service.LastRequest.Targets, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void LaunchProjectile_ForwardsSourcePositionAndLaunchParameters()
        {
            GameObject owner = new("EnemyProjectileAbilityBehavior");
            ProjectileProfileSO profile = ScriptableObject.CreateInstance<ProjectileProfileSO>();
            RecordingProjectileLauncher launcher = new();
            EnemyCombatAbility ability = owner.AddComponent<EnemyCombatAbility>();
            Vector3 direction = new(1f, 0.5f, -2f);
            SetField(ability, "_projectileLauncher", launcher);

            try
            {
                bool accepted = ability.LaunchProjectile(
                    profile,
                    25f,
                    direction,
                    projectileCount: 3,
                    penetrationCount: 4);

                Assert.That(accepted, Is.True);
                Assert.That(launcher.CallCount, Is.EqualTo(1));
                Assert.That(launcher.LastRequest.SourceInstanceId, Is.EqualTo(ability.GetInstanceID()));
                Assert.That(launcher.LastRequest.ProjectileProfile, Is.SameAs(profile));
                Assert.That(launcher.LastRequest.OriginPosition, Is.EqualTo(owner.transform.position));
                Assert.That(launcher.LastRequest.LaunchDirection, Is.EqualTo(direction));
                Assert.That(launcher.LastRequest.DamageAmount, Is.EqualTo(25f));
                Assert.That(launcher.LastRequest.ProjectileCount, Is.EqualTo(3));
                Assert.That(launcher.LastRequest.PenetrationCount, Is.EqualTo(4));
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(profile);
            }
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }

        private sealed class RecordingSkillExecutionService : ISkillExecutionService
        {
            public SkillExecutionRequest LastRequest { get; private set; }
            public int CallCount { get; private set; }

            public SkillExecutionResult Execute(in SkillExecutionRequest request)
            {
                LastRequest = request;
                CallCount++;
                return new SkillExecutionResult(true, default, 1);
            }
        }

        private sealed class RecordingProjectileLauncher : IProjectileLauncher
        {
            public ProjectileLaunchRequest LastRequest { get; private set; }
            public int CallCount { get; private set; }

            public bool Launch(in ProjectileLaunchRequest request)
            {
                LastRequest = request;
                CallCount++;
                return true;
            }

            public bool Launch(in AttackData attackData)
            {
                return false;
            }
        }

        private sealed class RecordingDamageable : IDamageable
        {
            public Vector3 Position => Vector3.zero;
            public bool IsAlive => true;
            public Transform Transform => null;
        }
    }
}
