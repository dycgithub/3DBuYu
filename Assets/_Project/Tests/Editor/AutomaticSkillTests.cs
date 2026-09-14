using System.Collections.Generic;
using CombatSystem;
using GameSystem;
using Interfaces;
using NUnit.Framework;
using Services;
using UnityEditor;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>验证自动目标规则与显式施法位置，并保持整卡内每个技能独立解析。</summary>
    public sealed class AutomaticSkillTests
    {
        [Test]
        public void SingleEnemyIgnoresSelectedTarget()
        {
            SkillDefinitionSO definition = SkillDefinitionSO.CreateRuntimeDefinition("auto", 10f);
            try
            {
                SerializedObject serialized = new(definition);
                serialized.FindProperty("targetSelectionMode").intValue = (int)SkillTargetSelectionMode.NearestEnemy;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Target near = new(Vector3.right);
                Target far = new(Vector3.right * 10f);
                SkillTargetResolver resolver = new(new Query(far, near));
                Assert.That(resolver.TryResolve(definition, Vector3.zero, out IReadOnlyList<IDamageable> targets, out _), Is.True);
                Assert.That(targets, Has.Count.EqualTo(1));
                Assert.That(targets[0], Is.SameAs(near));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void EachCardSkillUsesAutoTarget()
        {
            SkillDefinitionSO firstSkill = SkillDefinitionSO.CreateRuntimeDefinition("first", 3f);
            SkillDefinitionSO secondSkill = SkillDefinitionSO.CreateRuntimeDefinition("second", 7f);
            ItemCombatDefinitionSO combat = ItemCombatDefinitionSO.CreateRuntime(
                CombatScope.Central,
                new SkillGrant(0f, 2, 0f));
            ItemDefinition itemDefinition = ItemDefinition.CreateRuntime(
                "auto-card",
                "自动卡",
                null,
                ItemShape.Single,
                combat);

            try
            {
                AbilityCardTestData.SetTargetMode(firstSkill, SkillTargetSelectionMode.NearestEnemy);
                AbilityCardTestData.SetTargetMode(secondSkill, SkillTargetSelectionMode.NearestEnemy);
                AbilityCardTestData.ConfigureItem(
                    itemDefinition,
                    string.Empty,
                    Color.white,
                    new[] { firstSkill, secondSkill });

                Target near = new(Vector3.right);
                Target far = new(Vector3.right * 10f);
                SkillHand hand = new();
                CentralSkillLoadout loadout = new(new ItemCombatDefinitionResolver(), hand);
                loadout.ReplaceItems(new[] { new ItemVM(itemDefinition) });
                TestDamageProcessor damage = new();
                using EnergyService energy = new();
                energy.Initialize(20f, 20f);
                SkillCardState card = hand.Skills[0];
                SkillActivationService service = new(
                    energy,
                    new TestCombatPhaseService(),
                    damage,
                    new Query(far, near),
                    hand,
                    new TestItemConsumer(),
                    new SkillExecutor(damage, new AttackInstanceIdGenerator()));

                SkillActivationResult result = service.TryActivate(card.CardId);

                Assert.That(result.Succeeded, Is.True);
                Assert.That(damage.Targets, Has.Count.EqualTo(2));
                Assert.That(damage.Targets[0], Is.SameAs(near));
                Assert.That(damage.Targets[1], Is.SameAs(near));
            }
            finally
            {
                Object.DestroyImmediate(itemDefinition);
                Object.DestroyImmediate(combat);
                Object.DestroyImmediate(firstSkill);
                Object.DestroyImmediate(secondSkill);
            }
        }

        private sealed class Target : IDamageable
        {
            public Vector3 Position { get; }
            public bool IsAlive => true;
            public Transform Transform => null;
            public Target(Vector3 position) => Position = position;
        }

        private sealed class Query : ISpatialQueryService
        {
            private readonly List<IDamageable> _targets;
            public Query(params IDamageable[] targets) => _targets = new(targets);
            public int Register(IDamageable entity, float radius, int layerMask) => -1;
            public void Unregister(int entityId) { }
            public void UpdatePosition(int entityId, Vector3 position) { }
            public IDamageable QueryNearest(Vector3 center, float radius, int layerMask) => null;
            public List<IDamageable> QueryRadius(Vector3 center, float radius, int layerMask) => _targets;
            public List<IDamageable> QueryAll(int layerMask) => _targets;
        }

        private sealed class TestCombatPhaseService : ICombatPhaseService
        {
            public bool CanPerformCombatActions => true;
        }

        private sealed class TestItemConsumer : ICombatItemConsumptionService
        {
            public bool CanConsumeItemInstance(int itemInstanceId) => true;

            public bool TryConsumeItemInstance(int itemInstanceId) => true;
        }

        private sealed class TestDamageProcessor : IDamageRequestProcessor
        {
            public readonly List<IDamageable> Targets = new();

            public bool TryApplyDamage(IDamageable target, in DamageRequest request, out DamageResult result)
            {
                Targets.Add(target);
                result = new DamageResult
                {
                    Outcome = DamageOutcome.Applied,
                    AppliedDamage = request.DamageAmount
                };
                return true;
            }
        }
    }
}
