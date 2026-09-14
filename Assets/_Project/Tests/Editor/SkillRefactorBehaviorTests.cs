using System;
using System.Collections.Generic;
using CombatSystem;
using GameSystem;
using Interfaces;
using NUnit.Framework;
using Services;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>从释放、目标快照和卡牌事件入口验证技能整理后的行为。</summary>
    public sealed class SkillRefactorBehaviorTests
    {
        private readonly List<UnityEngine.Object> _objects = new();

        /// <summary>释放测试创建的配置资产。</summary>
        [TearDown]
        public void TearDown()
        {
            foreach (UnityEngine.Object value in _objects)
            {
                UnityEngine.Object.DestroyImmediate(value);
            }
            _objects.Clear();
        }

        /// <summary>冷却计时不逐帧通知表现，仅在结束时通知一次。</summary>
        [Test]
        public void CooldownNotifiesOnCompletion()
        {
            SkillCardState card = CreateCard(0f, 2, 1f);
            var hand = new SkillHand();
            hand.Rebuild(new[] { card });
            int notifications = 0;
            hand.SkillStateChanged += _ => notifications++;
            card.BeginCooldown();

            hand.AdvanceCooldowns(0.25f);
            Assert.That(card.RemainingCooldownSeconds, Is.EqualTo(0.75f));
            Assert.That(notifications, Is.Zero);
            hand.AdvanceCooldowns(0.75f);
            hand.AdvanceCooldowns(1f);
            Assert.That(notifications, Is.EqualTo(1));
        }

        /// <summary>提示和释放都先报告能量不足，且只读检查不会消费资源。</summary>
        [Test]
        public void AvailabilityMatchesActivation()
        {
            SkillCardState card = CreateCard(5f, 2, 1f);
            var hand = new SkillHand();
            hand.Rebuild(new[] { card });
            using var energy = new EnergyService();
            energy.Initialize(2f, 10f);
            var damage = new DamageRecorder();
            var consumer = new ItemConsumer();
            var query = new ReusedQuery();
            SkillActivationService service = CreateService(hand, energy, damage, consumer, query);

            Assert.That(service.CheckAvailability(card.CardId), Is.EqualTo(
                SkillActivationFailureReason.InsufficientEnergy));
            Assert.That(service.TryActivate(card.CardId).FailureReason, Is.EqualTo(
                SkillActivationFailureReason.InsufficientEnergy));
            Assert.That(query.QueryCount, Is.Zero);
            Assert.That(energy.CurrentEnergy.CurrentValue, Is.EqualTo(2f));
            Assert.That(card.RemainingUses, Is.EqualTo(2));
            Assert.That(consumer.ConsumptionCount, Is.Zero);
        }

        /// <summary>空间查询复用缓存时，下一次查询不能改掉本次施法快照。</summary>
        [Test]
        public void TargetsSurviveNextQuery()
        {
            SkillDefinitionSO definition = Track(SkillDefinitionSO.CreateRuntimeDefinition("snapshot", 1f));
            var first = new Target(Vector3.right);
            var second = new Target(Vector3.left);
            var query = new ReusedQuery { Next = new[] { first, first } };
            var resolver = new SkillTargetResolver(query);

            Assert.That(resolver.TryResolve(definition, Vector3.zero, out var snapshot, out _), Is.True);
            query.Next = new[] { second };
            Assert.That(resolver.HasTargets(definition, Vector3.zero, out _), Is.True);
            Assert.That(snapshot, Has.Count.EqualTo(1));
            Assert.That(snapshot[0], Is.SameAs(first));
        }

        /// <summary>伤害回调引起再次释放和手牌重建时，不重复消费或破坏当前释放。</summary>
        [Test]
        public void DamageCallbackCannotReenter()
        {
            SkillCardState card = CreateCard(2f, 2, 0f);
            var hand = new SkillHand();
            hand.Rebuild(new[] { card });
            using var energy = new EnergyService();
            energy.Initialize(10f, 10f);
            var damage = new DamageRecorder();
            var query = new ReusedQuery { Next = new[] { new Target(Vector3.one) } };
            SkillActivationService service = CreateService(hand, energy, damage, new ItemConsumer(), query);
            SkillActivationResult nested = default;
            damage.Applied = () =>
            {
                nested = service.TryActivate(card.CardId);
                hand.Rebuild(Array.Empty<SkillCardState>());
            };

            SkillActivationResult result = service.TryActivate(card.CardId);
            Assert.That(result.Succeeded, Is.True);
            Assert.That(nested.Succeeded, Is.False);
            Assert.That(damage.Count, Is.EqualTo(1));
            Assert.That(energy.CurrentEnergy.CurrentValue, Is.EqualTo(8f));
            Assert.That(card.RemainingUses, Is.EqualTo(1));
        }

        /// <summary>最后一次使用耗尽能量时，终局回调观察到的是完整提交后的卡牌状态。</summary>
        [Test]
        public void DepletionFollowsCardCommit()
        {
            SkillCardState card = CreateCard(10f, 1, 0f);
            var hand = new SkillHand();
            hand.Rebuild(new[] { card });
            using var energy = new EnergyService();
            energy.Initialize(10f, 10f);
            var damage = new DamageRecorder();
            var consumer = new ItemConsumer();
            var query = new ReusedQuery { Next = new[] { new Target(Vector3.one) } };
            SkillActivationService service = CreateService(hand, energy, damage, consumer, query);
            int notifications = 0;
            energy.EnergyDepleted += () =>
            {
                notifications++;
                Assert.That(damage.Count, Is.EqualTo(1));
                Assert.That(consumer.ConsumptionCount, Is.EqualTo(1));
                Assert.That(card.RemainingUses, Is.Zero);
                Assert.That(hand.FindByCardId(card.CardId), Is.Null);
            };

            Assert.That(service.TryActivate(card.CardId).Succeeded, Is.True);
            Assert.That(notifications, Is.EqualTo(1));
        }

        /// <summary>只有消费配置没有技能数组时，物品不产生技能卡。</summary>
        [Test]
        public void EmptySkillListCreatesNoCard()
        {
            var combat = Track(ItemCombatDefinitionSO.CreateRuntime(
                CombatScope.Central, new SkillGrant(2f, 3, 1f)));
            var item = Track(ItemDefinition.CreateRuntime("empty", "空技能", null, ItemShape.Single, combat));
            var hand = new SkillHand();
            new CentralSkillLoadout(new ItemCombatDefinitionResolver(), hand).ReplaceItems(new[] { new ItemVM(item) });
            Assert.That(hand.Skills, Is.Empty);
        }

        /// <summary>创建独立来源的测试卡，显式提供技能数组和共享消费配置。</summary>
        private SkillCardState CreateCard(float cost, int uses, float cooldown)
        {
            var definition = Track(SkillDefinitionSO.CreateRuntimeDefinition("card", 3f));
            var item = Track(ItemDefinition.CreateRuntime("item", "物品", null, ItemShape.Single));
            return SkillCardState.Create(17, item, new SkillGrant(cost, uses, cooldown), new[] { definition });
        }

        /// <summary>将测试依赖组装到与生产一致的唯一执行器。</summary>
        private static SkillActivationService CreateService(
            SkillHand hand, EnergyService energy, DamageRecorder damage, ItemConsumer consumer, ReusedQuery query)
        {
            return new SkillActivationService(energy, new Phase(), damage, query, hand, consumer,
                new SkillExecutor(damage, new AttackInstanceIdGenerator()));
        }

        /// <summary>登记需要释放的测试对象。</summary>
        private T Track<T>(T value) where T : UnityEngine.Object
        {
            _objects.Add(value);
            return value;
        }

        private sealed class Phase : ICombatPhaseService
        {
            public bool CanPerformCombatActions => true;
        }

        private sealed class ItemConsumer : ICombatItemConsumptionService
        {
            public int ConsumptionCount { get; private set; }
            public bool CanConsumeItemInstance(int id) => true;
            public bool TryConsumeItemInstance(int id)
            {
                ConsumptionCount++;
                return true;
            }
        }

        private sealed class DamageRecorder : IDamageRequestProcessor
        {
            public Action Applied;
            public int Count { get; private set; }
            public bool TryApplyDamage(IDamageable target, in DamageRequest request, out DamageResult result)
            {
                Count++;
                result = new DamageResult { Outcome = DamageOutcome.Applied, AppliedDamage = request.DamageAmount };
                Applied?.Invoke();
                return true;
            }
        }

        private sealed class Target : IDamageable
        {
            public Vector3 Position { get; }
            public bool IsAlive => true;
            public Transform Transform => null;
            public Target(Vector3 position) => Position = position;
        }

        private sealed class ReusedQuery : ISpatialQueryService
        {
            private readonly List<IDamageable> _cache = new();
            public IReadOnlyList<IDamageable> Next = Array.Empty<IDamageable>();
            public int QueryCount { get; private set; }
            public int Register(IDamageable entity, float radius, int layerMask) => -1;
            public void Unregister(int entityId) { }
            public void UpdatePosition(int entityId, Vector3 position) { }
            public IDamageable QueryNearest(Vector3 center, float radius, int layerMask) => null;
            public List<IDamageable> QueryRadius(Vector3 center, float radius, int layerMask) => QueryAll(layerMask);
            public List<IDamageable> QueryAll(int layerMask)
            {
                QueryCount++;
                _cache.Clear();
                _cache.AddRange(Next);
                return _cache;
            }
        }
    }
}
