using System;
using System.Collections.Generic;
using CombatSystem;
using GameSystem;
using Interfaces;
using NUnit.Framework;
using R3;
using Services;
using UnityEngine;

namespace CombatSystem.Tests
{
    /// <summary>
    /// 验证来源消费失败时退还能量，并保持独立卡牌状态和伤害批次未提交。
    /// </summary>
    public sealed class SkillActivationConsumptionTransactionTests
    {
        private readonly List<UnityEngine.Object> _temporaryObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int index = 0; index < _temporaryObjects.Count; index++)
            {
                if (_temporaryObjects[index] != null)
                {
                    UnityEngine.Object.DestroyImmediate(_temporaryObjects[index]);
                }
            }

            _temporaryObjects.Clear();
        }

        [Test]
        public void FailedSourceConsumption_DoesNotCommitSkillEffects()
        {
            SkillDefinitionSO definition = Track(
                SkillDefinitionSO.CreateRuntimeDefinition("consumption-failure", 10f));
            ItemVM item = new(new[] { Vector2Int.zero });
            SkillGrant grant = new(4f, 1, 3f);
            SkillCardState card = SkillCardState.Create(item.InstanceId, item.Definition, grant, new[] { definition });
            var hand = new SkillHand();
            hand.Rebuild(new[] { card });
            var target = new TestDamageable();
            var damageProcessor = new RecordingDamageProcessor();
            using var energy = new EnergyService();
            energy.Initialize(10f, 10f);
            var consumer = new FailingItemConsumer();
            var service = new SkillActivationService(
                energy,
                new CombatPhase(),
                damageProcessor,
                new SpatialQuery(target),
                hand,
                consumer,
                new SkillExecutor(damageProcessor, new AttackInstanceIdGenerator()));

            SkillActivationResult result = service.TryActivate(card.CardId);

            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(
                SkillActivationFailureReason.SourceConsumptionFailed));
            Assert.That(energy.CurrentEnergy.CurrentValue, Is.EqualTo(10f));
            Assert.That(damageProcessor.Requests, Is.Empty);
            Assert.That(card.RemainingUses, Is.EqualTo(1));
            Assert.That(card.RemainingCooldownSeconds, Is.Zero);
            Assert.That(consumer.ConsumeCount, Is.EqualTo(1));
        }

        private T Track<T>(T temporaryObject) where T : UnityEngine.Object
        {
            _temporaryObjects.Add(temporaryObject);
            return temporaryObject;
        }

        private sealed class CombatPhase : ICombatPhaseService
        {
            public bool CanPerformCombatActions => true;
        }

        private sealed class FailingItemConsumer : ICombatItemConsumptionService
        {
            public int ConsumeCount { get; private set; }

            public bool CanConsumeItemInstance(int itemInstanceId) => true;

            public bool TryConsumeItemInstance(int itemInstanceId)
            {
                ConsumeCount++;
                return false;
            }
        }

        private sealed class SpatialQuery : ISpatialQueryService
        {
            private readonly IDamageable _target;

            public SpatialQuery(IDamageable target)
            {
                _target = target;
            }

            public int Register(IDamageable entity, float radius, int layerMask) => -1;
            public void Unregister(int entityId) { }
            public void UpdatePosition(int entityId, Vector3 position) { }
            public IDamageable QueryNearest(Vector3 center, float radius, int layerMask) => _target;
            public List<IDamageable> QueryRadius(Vector3 center, float radius, int layerMask)
                => new() { _target };
            public List<IDamageable> QueryAll(int layerMask) => new() { _target };
        }

        private sealed class RecordingDamageProcessor : IDamageRequestProcessor
        {
            public List<DamageRequest> Requests { get; } = new();

            public bool TryApplyDamage(
                IDamageable target,
                in DamageRequest request,
                out DamageResult result)
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
            public Vector3 Position => Vector3.zero;
            public bool IsAlive => true;
            public Transform Transform => null;
        }
    }
}
