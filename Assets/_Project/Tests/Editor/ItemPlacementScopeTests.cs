using CombatSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using _Project.UI.Common;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证物品 ID 的战斗范围、网格摆放权限和两侧装备同步边界。
    /// </summary>
    public sealed class ItemPlacementScopeTests
    {
        [TestCase("001", CombatScope.CentralAndTransmitter)]
        [TestCase("002", CombatScope.Transmitter)]
        [TestCase("003", CombatScope.Transmitter)]
        [TestCase("004", CombatScope.Transmitter)]
        [TestCase("005", CombatScope.Transmitter)]
        [TestCase("006", CombatScope.Transmitter)]
        [TestCase("007", CombatScope.Transmitter)]
        [TestCase("008", CombatScope.Transmitter)]
        [TestCase("009", CombatScope.Transmitter)]
        [TestCase("010", CombatScope.CentralAndTransmitter)]
        [TestCase("011", CombatScope.Transmitter)]
        [TestCase("012", CombatScope.Central)]
        [TestCase("013", CombatScope.CentralAndTransmitter)]
        [TestCase("014", CombatScope.Transmitter)]
        [TestCase("015", CombatScope.Central)]
        public void FinalItemIdsMapToCombatScopes(string itemId, CombatScope expectedScope)
        {
            ItemDefinition item = CreateItem(itemId);

            try
            {
                CombatScope actualScope = new ItemCombatDefinitionResolver().ResolveCombatScope(item);

                Assert.That(actualScope, Is.EqualTo(expectedScope));
            }
            finally
            {
                Object.DestroyImmediate(item);
            }
        }

        [TestCase("001", true, true)]
        [TestCase("002", false, true)]
        [TestCase("003", false, true)]
        [TestCase("004", false, true)]
        [TestCase("005", false, true)]
        [TestCase("006", false, true)]
        [TestCase("007", false, true)]
        [TestCase("008", false, true)]
        [TestCase("009", false, true)]
        [TestCase("010", true, true)]
        [TestCase("011", false, true)]
        [TestCase("012", true, false)]
        [TestCase("013", true, true)]
        [TestCase("014", false, true)]
        [TestCase("015", true, false)]
        public void FinalItemIdsAllowOnlyConfiguredCombatGrids(
            string itemId,
            bool canEnterCentral,
            bool canEnterTransmitter)
        {
            ItemDefinition definition = CreateItem(itemId);
            ItemVM item = new ItemVM(definition);
            GridVM centralGrid = new GridVM(1, 1, GridType.CentralBackpack);
            GridVM transmitterGrid = new GridVM(1, 1, GridType.TransmitterBackpack);

            try
            {
                Assert.That(centralGrid.CanPlace(item, 0, 0), Is.EqualTo(canEnterCentral), itemId);
                Assert.That(transmitterGrid.CanPlace(item, 0, 0), Is.EqualTo(canEnterTransmitter), itemId);
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void WrongCombatGridIsRejectedOutsideHighlighting()
        {
            ItemDefinition definition = CreateItem("002");
            ItemVM item = new ItemVM(definition);
            GridVM centralGrid = new GridVM(1, 1, GridType.CentralBackpack);
            InventoryPlacementConfig config = ScriptableObject.CreateInstance<InventoryPlacementConfig>();

            try
            {
                Assert.That(centralGrid.Place(item, 0, 0), Is.False);
                Assert.That(centralGrid.ItemCount, Is.Zero);
                Assert.That(
                    config.Evaluate(centralGrid, item, 0, 0, out PlacementBlockReason reason),
                    Is.False);
                Assert.That(reason, Is.EqualTo(PlacementBlockReason.WrongGridType));
            }
            finally
            {
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(definition);
            }
        }

        [TestCase(CombatScope.Central, true, false)]
        [TestCase(CombatScope.Transmitter, false, true)]
        [TestCase(CombatScope.CentralAndTransmitter, true, true)]
        public void CombatScopeControlsLoadoutPermissions(
            CombatScope scope,
            bool createsCentralSkill,
            bool createsTransmitterModifier)
        {
            SkillDefinitionSO skill = SkillDefinitionSO.CreateRuntimeDefinition("scope-test", 1f);
            ItemCombatDefinitionSO combat = ItemCombatDefinitionSO.CreateRuntime(
                scope,
                new SkillGrant(0f, 1, 0f));
            SetTransmitterBonus(combat, 7f);
            ItemDefinition definition = ItemDefinition.CreateRuntime(
                "900",
                "范围测试",
                null,
                ItemShape.Single,
                combat,
                skill);
            ItemVM item = new ItemVM(definition);
            SkillHand hand = new SkillHand();
            CentralSkillLoadout centralLoadout = new CentralSkillLoadout(
                new ItemCombatDefinitionResolver(),
                hand);
            TransmitterAttackLoadout transmitterLoadout = new TransmitterAttackLoadout(
                new ItemCombatDefinitionResolver());

            try
            {
                centralLoadout.ReplaceItems(new[] { item });
                transmitterLoadout.ReplaceItems("scope-test", new[] { item });
                TransmitterAttackModifiers modifiers = TransmitterAttackModifiers.Default;
                transmitterLoadout.CollectAttackModifiers("scope-test", ref modifiers);

                Assert.That(hand.Skills, Has.Count.EqualTo(createsCentralSkill ? 1 : 0));
                Assert.That(modifiers.AdditiveDamageBonus, Is.EqualTo(createsTransmitterModifier ? 7f : 0f));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(combat);
                Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void FirstItemCanEnterTransmitterButHasNoTransmitterEffect()
        {
            SkillDefinitionSO skill = SkillDefinitionSO.CreateRuntimeDefinition("first-item", 1f);
            ItemCombatDefinitionSO combat = ItemCombatDefinitionSO.CreateRuntime(
                CombatScope.CentralAndTransmitter,
                new SkillGrant(0f, 1, 0f));
            SetTransmitterBonus(combat, 0f);
            ItemDefinition definition = ItemDefinition.CreateRuntime(
                "001",
                "首件物品",
                null,
                ItemShape.Single,
                combat,
                skill);
            ItemVM item = new ItemVM(definition);
            GridVM transmitterGrid = new GridVM(1, 1, GridType.TransmitterBackpack);
            TransmitterAttackLoadout loadout = new TransmitterAttackLoadout(
                new ItemCombatDefinitionResolver());

            try
            {
                Assert.That(transmitterGrid.Place(item, 0, 0), Is.True);
                loadout.ReplaceItems("first-port", new[] { item });
                TransmitterAttackModifiers modifiers = TransmitterAttackModifiers.Default;
                loadout.CollectAttackModifiers("first-port", ref modifiers);

                Assert.That(modifiers.AdditiveDamageBonus, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(combat);
                Object.DestroyImmediate(skill);
            }
        }

        [Test]
        public void EmptySideDoesNotSyncOrCreateSkill()
        {
            SkillDefinitionSO transmitterSkill = SkillDefinitionSO.CreateRuntimeDefinition("transmitter-only", 1f);
            ItemCombatDefinitionSO transmitterCombat = ItemCombatDefinitionSO.CreateRuntime(
                CombatScope.Transmitter,
                new SkillGrant(0f, 1, 0f));
            ItemDefinition transmitterDefinition = ItemDefinition.CreateRuntime(
                "002",
                "发射器物品",
                null,
                ItemShape.Single,
                transmitterCombat,
                transmitterSkill);
            ItemVM transmitterItem = new ItemVM(transmitterDefinition);

            ItemCombatDefinitionSO centralCombat = ItemCombatDefinitionSO.CreateRuntime(
                CombatScope.Central,
                new SkillGrant(0f, 1, 0f));
            SetTransmitterBonus(centralCombat, 9f);
            ItemDefinition centralDefinition = ItemDefinition.CreateRuntime(
                "012",
                "中心物品",
                null,
                ItemShape.Single,
                centralCombat);
            ItemVM centralItem = new ItemVM(centralDefinition);
            SkillHand hand = new SkillHand();
            CentralSkillLoadout centralLoadout = new CentralSkillLoadout(
                new ItemCombatDefinitionResolver(),
                hand);
            TransmitterAttackLoadout transmitterLoadout = new TransmitterAttackLoadout(
                new ItemCombatDefinitionResolver());

            try
            {
                centralLoadout.ReplaceItems(new[] { transmitterItem });
                transmitterLoadout.ReplaceItems("central-port", new[] { centralItem });
                TransmitterAttackModifiers modifiers = TransmitterAttackModifiers.Default;
                transmitterLoadout.CollectAttackModifiers("central-port", ref modifiers);

                Assert.That(hand.Skills, Is.Empty);
                Assert.That(modifiers.AdditiveDamageBonus, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(transmitterDefinition);
                Object.DestroyImmediate(transmitterCombat);
                Object.DestroyImmediate(transmitterSkill);
                Object.DestroyImmediate(centralDefinition);
                Object.DestroyImmediate(centralCombat);
            }
        }

        [Test]
        public void UnconfiguredFinalItemHasNoImplicitTransmitterBonus()
        {
            ItemDefinition definition = CreateItem("002");
            ItemVM item = new ItemVM(definition);
            TransmitterAttackLoadout loadout = new TransmitterAttackLoadout(
                new ItemCombatDefinitionResolver());

            try
            {
                loadout.ReplaceItems("unconfigured-port", new[] { item });
                TransmitterAttackModifiers modifiers = TransmitterAttackModifiers.Default;
                loadout.CollectAttackModifiers("unconfigured-port", ref modifiers);

                Assert.That(modifiers.AdditiveDamageBonus, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void UnconfiguredFinalItemHasNoImplicitTooltipEffect()
        {
            ItemDefinition definition = CreateItem("002");
            ItemVM item = new ItemVM(definition);

            try
            {
                ItemTooltipContent content = ItemTooltipTextBuilder.Build(
                    item,
                    GridType.TransmitterBackpack,
                    0);

                Assert.That(content.Scope, Is.Empty);
                Assert.That(content.Effects, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        [Test]
        public void ZeroTransmitterBonusIsNotShownAsEffect()
        {
            ItemCombatDefinitionSO combat = ItemCombatDefinitionSO.CreateRuntime(
                CombatScope.CentralAndTransmitter,
                null);
            ItemDefinition definition = ItemDefinition.CreateRuntime(
                "001",
                "中心技能",
                null,
                ItemShape.Plus5,
                combat);
            ItemVM item = new ItemVM(definition);

            try
            {
                ItemTooltipContent content = ItemTooltipTextBuilder.Build(
                    item,
                    GridType.TransmitterBackpack,
                    0);

                Assert.That(content.Effects, Does.Not.Contain("发射器伤害"));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(combat);
            }
        }

        [Test]
        public void ExplicitFallbackStillProvidesItsConfiguredBonus()
        {
            ItemDefinition definition = ItemDefinition.CreateRuntime(
                "fallback-test",
                "显式回退",
                null,
                ItemShape.Single,
                ItemCombatDefinitionSO.RuntimeFallback);
            ItemVM item = new ItemVM(definition);
            TransmitterAttackLoadout loadout = new TransmitterAttackLoadout(
                new ItemCombatDefinitionResolver());

            try
            {
                loadout.ReplaceItems("fallback-port", new[] { item });
                TransmitterAttackModifiers modifiers = TransmitterAttackModifiers.Default;
                loadout.CollectAttackModifiers("fallback-port", ref modifiers);

                Assert.That(modifiers.AdditiveDamageBonus, Is.EqualTo(50f));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        private static ItemDefinition CreateItem(string itemId)
        {
            return ItemDefinition.CreateRuntime(
                itemId,
                itemId,
                null,
                ItemShape.Single);
        }

        private static void SetTransmitterBonus(ItemCombatDefinitionSO definition, float bonus)
        {
            SerializedObject serialized = new SerializedObject(definition);
            serialized.FindProperty("transmitterAdditiveDamageBonus").floatValue = bonus;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
