using System.Collections.Generic;
using CombatSystem;
using CameraSystem;
using GameSystem;
using Interfaces;
using NUnit.Framework;
using Services;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 锁定独立卡牌状态、来源物品消费和统一伤害入口的行为。
    /// </summary>
    public sealed class AbilityHardSwitchContractTests
    {
        private readonly List<Object> _runtimeObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int index = _runtimeObjects.Count - 1; index >= 0; index--)
            {
                if (_runtimeObjects[index] != null)
                {
                    Object.DestroyImmediate(_runtimeObjects[index]);
                }
            }

            _runtimeObjects.Clear();
        }

        [Test]
        public void DifferentInstancesMakeSeparateCards()
        {
            SkillDefinitionSO definition = CreateSkill("shared", 10f);
            ItemCombatDefinitionSO combat = CreateCombat(definition, uses: 2);
            ItemDefinition itemDefinition = CreateItem("shared-item", combat, definition);
            ItemVM firstItem = new(itemDefinition);
            ItemVM secondItem = new(itemDefinition);
            SkillHand hand = new();
            CentralSkillLoadout loadout = new(new ItemCombatDefinitionResolver(), hand);

            loadout.ReplaceItems(new[] { firstItem, secondItem });

            Assert.That(hand.Skills, Has.Count.EqualTo(2));
            Assert.That(hand.Skills[0].CardId, Is.Not.EqualTo(hand.Skills[1].CardId));
            Assert.That(hand.Skills[0].SourceItemInstanceId, Is.EqualTo(firstItem.InstanceId));
            Assert.That(hand.Skills[1].SourceItemInstanceId, Is.EqualTo(secondItem.InstanceId));
            Assert.That(hand.Skills[0].SourceItemDefinition, Is.SameAs(itemDefinition));
            Assert.That(hand.Skills[1].SourceItemDefinition, Is.SameAs(itemDefinition));
            Assert.That(hand.Skills[0].RemainingUses, Is.EqualTo(2));
            Assert.That(hand.Skills[1].RemainingUses, Is.EqualTo(2));

            Assert.That(hand.Skills[0].ConsumeUse(), Is.True);

            Assert.That(hand.Skills[0].RemainingUses, Is.EqualTo(1));
            Assert.That(hand.Skills[1].RemainingUses, Is.EqualTo(2));
        }

        [Test]
        public void LastSkillUseConsumesItsSourceItem()
        {
            SkillDefinitionSO definition = CreateSkill("limited", 10f);
            ItemCombatDefinitionSO combat = CreateCombat(definition, uses: 2);
            ItemDefinition itemDefinition = CreateItem("item", combat, definition);
            ItemVM item = new(itemDefinition);
            SkillHand hand = new();
            CentralSkillLoadout loadout = new(new ItemCombatDefinitionResolver(), hand);
            loadout.ReplaceItems(new[] { item });
            SkillCardState skill = hand.Skills[0];
            TestDamageable target = new(Vector3.forward);
            TestDamageProcessor damage = new();
            TestItemConsumer consumer = new(item.InstanceId);
            EnergyService energy = CreateEnergy(20f);
            SkillActivationService service = new(
                energy,
                new TestCombatPhaseService(),
                damage,
                new TestSpatialQueryService(target),
                hand,
                consumer,
                new SkillExecutor(damage, new AttackInstanceIdGenerator()));

            Assert.That(service.TryActivate(skill.CardId).Succeeded, Is.True);
            Assert.That(skill.RemainingUses, Is.EqualTo(1));
            Assert.That(consumer.ConsumeCount, Is.Zero);
            Assert.That(hand.FindByCardId(skill.CardId), Is.SameAs(skill));
            Assert.That(damage.Requests[0].SourceInstanceId, Is.EqualTo(item.InstanceId));

            Assert.That(service.TryActivate(skill.CardId).Succeeded, Is.True);
            Assert.That(consumer.ConsumeCount, Is.EqualTo(1));
            Assert.That(hand.FindByCardId(skill.CardId), Is.Null);
            Assert.That(damage.Requests, Has.Count.EqualTo(2));
            energy.Dispose();
        }

        [Test]
        public void DamageProcessorDoesNotFallbackToLegacyDamageableMethod()
        {
            DamageRequestProcessor processor = new();
            TestDamageable target = new(Vector3.forward);
            DamageRequest request = new()
            {
                DamageAmount = 10f,
                DamageType = DamageType.Physical,
                HitPosition = target.Position,
                HitNormal = Vector3.forward
            };

            Assert.That(processor.TryApplyDamage(target, request, out DamageResult result), Is.False);
            Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Invalid));
        }

        [Test]
        public void RegionalCameraDistanceUsesCoverageRadius()
        {
            float distance = SphericalCameraRegional.CalculateCameraDistance(25f, 30f, 20f);

            float expected = 25f * Mathf.Sin(50f * Mathf.Deg2Rad) /
                Mathf.Sin(30f * Mathf.Deg2Rad);
            Assert.That(distance, Is.EqualTo(expected).Within(0.001f));
        }

        private SkillDefinitionSO CreateSkill(string id, float damage)
        {
            SkillDefinitionSO definition = SkillDefinitionSO.CreateRuntimeDefinition(id, damage);
            _runtimeObjects.Add(definition);
            return definition;
        }

        private ItemCombatDefinitionSO CreateCombat(SkillDefinitionSO skill, int uses)
        {
            ItemCombatDefinitionSO definition = ItemCombatDefinitionSO.CreateRuntime(
                CombatScope.Central,
                new SkillGrant(1f, uses, 0f));
            _runtimeObjects.Add(definition);
            return definition;
        }

        private ItemDefinition CreateItem(
            string id,
            ItemCombatDefinitionSO combat,
            params SkillDefinitionSO[] skills)
        {
            ItemDefinition definition = ItemDefinition.CreateRuntime(
                id,
                id,
                null,
                ItemShape.Single,
                combat);
            _runtimeObjects.Add(definition);
            AbilityCardTestData.ConfigureItem(definition, string.Empty, Color.white, skills);
            return definition;
        }

        private static EnergyService CreateEnergy(float amount)
        {
            EnergyService energy = new();
            energy.Initialize(amount, amount);
            return energy;
        }

        private sealed class TestCombatPhaseService : ICombatPhaseService
        {
            public bool CanPerformCombatActions => true;
        }

        private sealed class TestItemConsumer : ICombatItemConsumptionService
        {
            private readonly int _itemInstanceId;

            public int ConsumeCount { get; private set; }

            public TestItemConsumer(int itemInstanceId)
            {
                _itemInstanceId = itemInstanceId;
            }

            public bool CanConsumeItemInstance(int itemInstanceId)
            {
                return itemInstanceId == _itemInstanceId;
            }

            public bool TryConsumeItemInstance(int itemInstanceId)
            {
                if (!CanConsumeItemInstance(itemInstanceId))
                {
                    return false;
                }

                ConsumeCount++;
                return true;
            }
        }

        private sealed class TestSpatialQueryService : ISpatialQueryService
        {
            private readonly List<IDamageable> _targets;

            public TestSpatialQueryService(params IDamageable[] targets)
            {
                _targets = new List<IDamageable>(targets);
            }

            public int Register(IDamageable entity, float radius, int layerMask) => -1;
            public void Unregister(int entityId) { }
            public void UpdatePosition(int entityId, Vector3 position) { }
            public IDamageable QueryNearest(Vector3 center, float radius, int layerMask) => null;
            public List<IDamageable> QueryRadius(Vector3 center, float radius, int layerMask) => _targets;
            public List<IDamageable> QueryAll(int layerMask) => _targets;
        }

        private sealed class TestDamageProcessor : IDamageRequestProcessor
        {
            public readonly List<DamageRequest> Requests = new();

            public bool TryApplyDamage(IDamageable target, in DamageRequest request, out DamageResult result)
            {
                Requests.Add(request);
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
            public Vector3 Position { get; }
            public bool IsAlive => true;
            public Transform Transform => null;

            public TestDamageable(Vector3 position)
            {
                Position = position;
            }
        }
    }
}
