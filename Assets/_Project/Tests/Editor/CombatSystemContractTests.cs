using System.Collections.Generic;
using CombatSystem;
using Interfaces;
using NUnit.Framework;
using Services;
using SpatialSystem.Bridge;
using UnityEngine;

public sealed class CombatSystemContractTests
{
    [Test]
    public void ItemInstanceId_IsPreservedByInventorySnapshot()
    {
        ItemDefinition definition = ItemDefinition.CreateRuntime(
            "test-item", "测试物品", null, ItemShape.Single);
        ItemVM source = new ItemVM(definition);
        InventoryItemSnapshot snapshot = new InventoryItemSnapshot(source);

        ItemVM restored = snapshot.CreateItem();

        Assert.That(restored.InstanceId, Is.EqualTo(source.InstanceId));
    }

    [Test]
    public void CentralItem_CreatesOneFiftyDamageCardByDefault()
    {
        ItemDefinition definition = ItemDefinition.CreateRuntime(
            "test-item", "测试物品", null, ItemShape.Single);
        ItemVM item = new ItemVM(definition);
        var hand = new SkillHand();
        var loadout = new CentralLoadout(new ItemCombatCatalog(), hand);

        loadout.SetItems(new[] { item });

        Assert.That(hand.Cards, Has.Count.EqualTo(1));
        Assert.That(hand.Cards[0].SourceItemInstanceId, Is.EqualTo(item.InstanceId));
        Assert.That(hand.Cards[0].Definition.Damage, Is.EqualTo(50f));
    }

    [Test]
    public void TransmitterItem_AddsFiftyBeforeOtherMultipliers()
    {
        ItemDefinition definition = ItemDefinition.CreateRuntime(
            "test-item", "测试物品", null, ItemShape.Single);
        ItemVM item = new ItemVM(definition);
        var loadout = new TransmitterLoadout(new ItemCombatCatalog());
        loadout.SetItems(0, new[] { item });
        var buildService = new TransmitterShootBuildService(loadout);
        BulletProfile profile = BulletProfile.CreateRuntime("test-profile");
        var input = new TransmitterAttackInput(
            1, 0, profile, Vector3.zero, Vector3.forward,
            1f, 1f, 1f, 1, 0, 0f, 1f);

        TransmitterShootBuild build = buildService.Build(in input);
        var builder = new AttackBuilder();

        Assert.That(builder.TryBuild(in input, in build, out AttackInfo attack), Is.True);
        Assert.That(attack.Damage, Is.EqualTo(60f));
    }

    [TestCase("Transmitter", 0)]
    [TestCase("Transmitter (1)", 1)]
    [TestCase("Transmitter (7)", 7)]
    [TestCase("TopLeft", 6)]
    public void TransmitterGridBinding_UsesTheConfirmedMapping(string name, int expectedIndex)
    {
        Assert.That(TransmitterGridBinding.ResolveIndex(name), Is.EqualTo(expectedIndex));
    }

    [Test]
    public void CentralAbility_DamagesAllQueriedEnemiesAndConsumesTheItemOnce()
    {
        ItemDefinition definition = ItemDefinition.CreateRuntime(
            "central-item", "中央物品", null, ItemShape.Single);
        ItemVM item = new ItemVM(definition);
        SkillHand hand = new SkillHand();
        hand.SetCards(new[]
        {
            new SkillCardRuntime(item.InstanceId, definition.Id, SkillDefinition.CreateRuntime("central", 50f))
        });

        var pointer = new SkillTargetPointer();
        var firstTarget = new TestDamageable(new Vector3(1f, 0f, 0f));
        var secondTarget = new TestDamageable(new Vector3(2f, 0f, 0f));
        pointer.Confirm(firstTarget);

        var spatial = new TestSpatialQueryService(firstTarget, secondTarget);
        var damage = new TestDamageApplier();
        var consumer = new TestItemConsumer(item.InstanceId);
        var energy = new GameSystem.EnergyService();
        energy.Initialize(100f, 100f);
        var service = new AbilityService(
            energy,
            new TestCombatPhaseService(),
            damage,
            spatial,
            hand,
            pointer,
            consumer);

        bool activated = service.TryActivate(item.InstanceId);

        Assert.That(activated, Is.True);
        Assert.That(spatial.QueriedLayerMask, Is.EqualTo(SpatialRegistry.LAYER_ENEMY));
        Assert.That(damage.Requests, Has.Count.EqualTo(2));
        Assert.That(damage.Requests[0].BaseDamage, Is.EqualTo(50f));
        Assert.That(damage.Requests[0].DamageType, Is.EqualTo(DamageType.Physical));
        Assert.That(consumer.ConsumeCount, Is.EqualTo(1));
        Assert.That(hand.Cards, Is.Empty);

        energy.Dispose();
    }

    private sealed class TestCombatPhaseService : ICombatPhaseService
    {
        public bool CanPerformCombatActions => true;
    }

    private sealed class TestItemConsumer : ICombatItemConsumer
    {
        private readonly int _itemInstanceId;

        public int ConsumeCount { get; private set; }

        public TestItemConsumer(int itemInstanceId)
        {
            _itemInstanceId = itemInstanceId;
        }

        public bool CanConsume(int itemInstanceId) => itemInstanceId == _itemInstanceId;

        public bool TryConsume(int itemInstanceId)
        {
            if (!CanConsume(itemInstanceId))
                return false;
            ConsumeCount++;
            return true;
        }
    }

    private sealed class TestSpatialQueryService : ISpatialQueryService
    {
        private readonly List<IDamageable> _targets;

        public int QueriedLayerMask { get; private set; }

        public TestSpatialQueryService(params IDamageable[] targets)
        {
            _targets = new List<IDamageable>(targets);
        }

        public int Register(IDamageable entity, float radius, int layerMask) => -1;
        public void Unregister(int entityId) { }
        public void UpdatePosition(int entityId, Vector3 position) { }
        public IDamageable QueryNearest(Vector3 center, float radius, int layerMask) => null;
        public List<IDamageable> QueryRadius(Vector3 center, float radius, int layerMask) => _targets;

        public List<IDamageable> QueryAll(int layerMask)
        {
            QueriedLayerMask = layerMask;
            return _targets;
        }
    }

    private sealed class TestDamageApplier : IDamageApplier
    {
        public readonly List<DamageRequest> Requests = new();

        public bool TryApply(IDamageable target, in DamageRequest request, out DamageResult result)
        {
            Requests.Add(request);
            result = new DamageResult
            {
                Outcome = DamageOutcome.Applied,
                ActualDamage = request.BaseDamage
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

        public void TakeDamage(float amount) { }
    }
}
