using System;
using System.Collections.Generic;
using CombatSystem;
using GameSystem;
using Interfaces;
using NUnit.Framework;
using R3;
using Services;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>验证整卡准备失败时不提交前序伤害，成功时只消费一次资源。</summary>
    public sealed class SkillActivationTransactionTests
    {
        private readonly List<UnityEngine.Object> _runtimeObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int index = _runtimeObjects.Count - 1; index >= 0; index--)
            {
                if (_runtimeObjects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_runtimeObjects[index]);
                }
            }

            _runtimeObjects.Clear();
        }

        [Test]
        public void CardSpendsEnergyOnce()
        {
            SkillDefinitionSO firstSkill = CreateSkill("first", 2f);
            SkillDefinitionSO secondSkill = CreateSkill("second", 3f);
            ItemCombatDefinitionSO combat = CreateCombat(firstSkill, energyCost: 7f, useCount: 2);
            ItemDefinition itemDefinition = CreateItem("energy-card", combat, firstSkill, secondSkill);
            ItemVM item = new(itemDefinition);
            SkillHand hand = new();
            CentralSkillLoadout loadout = new(new ItemCombatDefinitionResolver(), hand);
            loadout.ReplaceItems(new[] { item });
            SkillCardState card = hand.Skills[0];
            TestDamageProcessor damage = new();
            using RecordingEnergyService energy = CreateEnergy(20f);
            TestItemConsumer consumer = new();
            SkillActivationService service = CreateService(energy, hand, damage, consumer);

            SkillActivationResult result = service.TryActivate(card.CardId);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(energy.SpendCount, Is.EqualTo(1));
            Assert.That(energy.SpentBaseAmounts, Is.EqualTo(new[] { 7f }));
            Assert.That(energy.CurrentEnergy.CurrentValue, Is.EqualTo(13f));
            Assert.That(damage.Requests, Has.Count.EqualTo(2));
            Assert.That(damage.Requests[0].SourceInstanceId, Is.EqualTo(item.InstanceId));
            Assert.That(damage.Requests[1].SourceInstanceId, Is.EqualTo(item.InstanceId));
            Assert.That(card.RemainingUses, Is.EqualTo(1));
            Assert.That(card.RemainingCooldownSeconds, Is.EqualTo(4f));
            Assert.That(result.RemainingUses, Is.EqualTo(1));
            Assert.That(result.RemainingCooldownSeconds, Is.EqualTo(4f));
            Assert.That(consumer.ConsumeCount, Is.Zero);
            Assert.That(hand.FindByCardId(card.CardId), Is.SameAs(card));
        }

        [Test]
        public void FailedCardHasNoPartialEffect()
        {
            SkillDefinitionSO firstSkill = CreateSkill("first", 10f);
            SkillDefinitionSO secondSkill = CreateSkill("fails", 20f);
            FailingSkillActionDefinitionSO failingAction = ScriptableObject.CreateInstance<FailingSkillActionDefinitionSO>();
            _runtimeObjects.Add(failingAction);
            AbilityCardTestData.SetAction(secondSkill, failingAction);
            ItemCombatDefinitionSO combat = CreateCombat(firstSkill, energyCost: 6f, useCount: 2);
            ItemDefinition itemDefinition = CreateItem("failed-card", combat, firstSkill, secondSkill);
            ItemVM item = new(itemDefinition);
            SkillHand hand = new();
            CentralSkillLoadout loadout = new(new ItemCombatDefinitionResolver(), hand);
            loadout.ReplaceItems(new[] { item });
            SkillCardState card = hand.Skills[0];
            TestDamageProcessor damage = new();
            using RecordingEnergyService energy = CreateEnergy(20f);
            TestItemConsumer consumer = new();
            SkillActivationService service = CreateService(energy, hand, damage, consumer);

            SkillActivationResult result = service.TryActivate(card.CardId);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(SkillActivationFailureReason.ActionFailed));
            Assert.That(damage.Requests, Is.Empty);
            Assert.That(energy.CurrentEnergy.CurrentValue, Is.EqualTo(20f));
            Assert.That(card.RemainingUses, Is.EqualTo(2));
            Assert.That(card.RemainingCooldownSeconds, Is.Zero);
            Assert.That(consumer.ConsumeCount, Is.Zero);
            Assert.That(hand.FindByCardId(card.CardId), Is.SameAs(card));
            Assert.That(item.InstanceId, Is.EqualTo(card.SourceItemInstanceId));
        }

        private SkillDefinitionSO CreateSkill(string identifier, float damage)
        {
            SkillDefinitionSO skill = SkillDefinitionSO.CreateRuntimeDefinition(identifier, damage);
            _runtimeObjects.Add(skill);
            return skill;
        }

        private ItemCombatDefinitionSO CreateCombat(
            SkillDefinitionSO skill,
            float energyCost,
            int useCount)
        {
            ItemCombatDefinitionSO combat = ItemCombatDefinitionSO.CreateRuntime(
                CombatScope.Central,
                new SkillGrant(energyCost, useCount, 4f));
            _runtimeObjects.Add(combat);
            return combat;
        }

        private ItemDefinition CreateItem(
            string id,
            ItemCombatDefinitionSO combat,
            params SkillDefinitionSO[] skills)
        {
            ItemDefinition item = ItemDefinition.CreateRuntime(
                id,
                id,
                null,
                ItemShape.Single,
                combat);
            _runtimeObjects.Add(item);
            AbilityCardTestData.ConfigureItem(item, string.Empty, Color.white, skills);
            return item;
        }

        private static RecordingEnergyService CreateEnergy(float amount)
        {
            RecordingEnergyService energy = new();
            energy.Initialize(amount, amount);
            return energy;
        }

        private static SkillActivationService CreateService(
            RecordingEnergyService energy,
            SkillHand hand,
            TestDamageProcessor damage,
            TestItemConsumer consumer)
        {
            return new SkillActivationService(
                energy,
                new TestCombatPhaseService(),
                damage,
                new TestSpatialQueryService(new TestTarget(Vector3.forward)),
                hand,
                consumer,
                new SkillExecutor(damage, new AttackInstanceIdGenerator()));
        }

        private sealed class TestCombatPhaseService : ICombatPhaseService
        {
            public bool CanPerformCombatActions => true;
        }

        private sealed class TestItemConsumer : ICombatItemConsumptionService
        {
            public int ConsumeCount { get; private set; }

            public bool CanConsumeItemInstance(int itemInstanceId) => true;

            public bool TryConsumeItemInstance(int itemInstanceId)
            {
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

        private sealed class TestTarget : IDamageable
        {
            public Vector3 Position { get; }
            public bool IsAlive => true;
            public Transform Transform => null;

            public TestTarget(Vector3 position)
            {
                Position = position;
            }
        }

        private sealed class RecordingEnergyService : IEnergyService, IEnergyActionScope, IDisposable
        {
            private readonly EnergyService _inner = new();

            public ReadOnlyReactiveProperty<float> CurrentEnergy => _inner.CurrentEnergy;
            public float MaximumEnergy => _inner.MaximumEnergy;
            public bool IsDepleted => _inner.IsDepleted;
            public int SpendCount { get; private set; }
            public List<float> SpentBaseAmounts { get; } = new();

            public event Action EnergyDepleted
            {
                add => _inner.EnergyDepleted += value;
                remove => _inner.EnergyDepleted -= value;
            }

            public void Initialize(float initialEnergy, float maximumEnergy)
                => _inner.Initialize(initialEnergy, maximumEnergy);

            public bool TrySpend(float baseAmount, EnergySpendKind kind)
            {
                SpendCount++;
                SpentBaseAmounts.Add(baseAmount);
                return _inner.TrySpend(baseAmount, kind);
            }

            public float Drain(float baseAmount, EnergySpendKind kind)
                => _inner.Drain(baseAmount, kind);

            public float AddEnergy(float amount)
                => _inner.AddEnergy(amount);

            public float Tick(float deltaTime, float baseDrainPerSecond)
                => _inner.Tick(deltaTime, baseDrainPerSecond);

            public IDisposable BeginAction() => _inner.BeginAction();

            public void Dispose() => _inner.Dispose();
        }
    }

    /// <summary>用于验证技能卡失败时不会提交前序技能效果。</summary>
    public sealed class FailingSkillActionDefinitionSO : SkillActionDefinitionSO
    {
        public override bool TryExecute(
            in SkillExecutionContext context,
            IReadOnlyList<IDamageable> targets,
            IDamageRequestProcessor damageProcessor,
            out int appliedTargetCount)
        {
            appliedTargetCount = 0;
            return false;
        }
    }
}
