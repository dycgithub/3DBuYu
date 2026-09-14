using System.Collections.Generic;
using CombatSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>验证用户确认的 001-015 道具目录、形状和战斗作用域。</summary>
    public sealed class ItemCatalogContentTests
    {
        private const string CatalogPath = "Assets/_Project/SO/Item/ItemCatalog.asset";

        [Test]
        public void CatalogContainsFinalItemSet()
        {
            ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
            Assert.That(catalog, Is.Not.Null, CatalogPath);
            Assert.That(catalog.Items, Has.Count.EqualTo(15));

            var expectedIds = new HashSet<string>();
            for (int index = 1; index <= 15; index++)
            {
                expectedIds.Add(index.ToString("000"));
            }

            var actualIds = new HashSet<string>();
            foreach (ItemDefinition item in catalog.Items)
            {
                Assert.That(item, Is.Not.Null);
                Assert.That(actualIds.Add(item.Id), Is.True, item.Id);
                Assert.That(item.DisplayName, Is.Not.Empty, item.Id);
                Assert.That(item.Description, Is.Not.Empty, item.Id);
                Assert.That(item.CombatDefinition, Is.Not.Null, item.Id);
            }

            Assert.That(actualIds, Is.EqualTo(expectedIds));
        }

        [TestCase("001", ItemShape.Plus5, CombatScope.CentralAndTransmitter)]
        [TestCase("002", ItemShape.Single, CombatScope.Transmitter)]
        [TestCase("003", ItemShape.Plus3, CombatScope.Transmitter)]
        [TestCase("004", ItemShape.Vertical2, CombatScope.Transmitter)]
        [TestCase("005", ItemShape.LShape1, CombatScope.Transmitter)]
        [TestCase("006", ItemShape.LShape2, CombatScope.Transmitter)]
        [TestCase("007", ItemShape.TShape1, CombatScope.Transmitter)]
        [TestCase("008", ItemShape.TShape2, CombatScope.Transmitter)]
        [TestCase("009", ItemShape.Square2x2, CombatScope.Transmitter)]
        [TestCase("010", ItemShape.Plus5, CombatScope.CentralAndTransmitter)]
        [TestCase("011", ItemShape.Horizontal2, CombatScope.Transmitter)]
        [TestCase("012", ItemShape.Plus5, CombatScope.Central)]
        [TestCase("013", ItemShape.Plus5, CombatScope.CentralAndTransmitter)]
        [TestCase("014", ItemShape.Horizontal2, CombatScope.Transmitter)]
        [TestCase("015", ItemShape.Plus3, CombatScope.Central)]
        public void ItemUsesConfirmedShapeAndScope(
            string itemId,
            ItemShape expectedShape,
            CombatScope expectedScope)
        {
            ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
            ItemDefinition item = catalog?.GetById(itemId);

            Assert.That(item, Is.Not.Null, itemId);
            Assert.That(item.Shape, Is.EqualTo(expectedShape), itemId);
            Assert.That(item.CombatDefinition.CombatScope, Is.EqualTo(expectedScope), itemId);
        }

        [Test]
        public void CentralItemsHaveCardsAndTransmitterOnlyItemsDoNot()
        {
            ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
            string[] centralIds = { "001", "010", "012", "013", "015" };

            foreach (ItemDefinition item in catalog.Items)
            {
                bool isCentralItem = System.Array.IndexOf(centralIds, item.Id) >= 0;
                Assert.That(item.SkillDefinitions.Length, isCentralItem ? Is.GreaterThan(0) : Is.EqualTo(0), item.Id);
            }
        }

        [Test]
        public void FirstItemHasNoTransmitterModifier()
        {
            ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
            ItemDefinition item = catalog?.GetById("001");

            Assert.That(item, Is.Not.Null);
            Assert.That(item.CombatDefinition.TransmitterAdditiveDamageBonus, Is.Zero);
            Assert.That(item.CombatDefinition.TransmitterAttackModifierDefinitions, Is.Empty);
        }

        [TestCase("003")]
        [TestCase("007")]
        [TestCase("010")]
        [TestCase("011")]
        [TestCase("013")]
        [TestCase("014")]
        public void SpecialTransmitterItemsHaveProjectileProfile(string itemId)
        {
            ItemCatalog catalog = AssetDatabase.LoadAssetAtPath<ItemCatalog>(CatalogPath);
            ItemDefinition item = catalog?.GetById(itemId);
            Assert.That(item, Is.Not.Null, itemId);

            SerializedObject serialized = new SerializedObject(item.CombatDefinition);
            SerializedProperty profile = serialized.FindProperty("transmitterProjectileProfile");
            Assert.That(profile, Is.Not.Null, itemId);
            Assert.That(profile.objectReferenceValue, Is.Not.Null, itemId);
        }

        [Test]
        public void SpecialProfileIsSelectedByTransmitterLoadout()
        {
            ProjectileProfileSO baseProfile = ProjectileProfileSO.CreateRuntimeProfile("base-profile");
            ProjectileProfileSO specialProfile = ProjectileProfileSO.CreateRuntimeProfile("special-profile");
            ItemCombatDefinitionSO combat = ItemCombatDefinitionSO.CreateRuntime(
                CombatScope.Transmitter,
                null);
            SerializedObject combatSerialized = new SerializedObject(combat);
            SerializedProperty profile = combatSerialized.FindProperty("transmitterProjectileProfile");
            Assert.That(profile, Is.Not.Null);
            profile.objectReferenceValue = specialProfile;
            combatSerialized.ApplyModifiedPropertiesWithoutUndo();

            ItemDefinition definition = ItemDefinition.CreateRuntime(
                "003",
                "追踪",
                null,
                ItemShape.Plus3,
                combat);
            ItemVM item = new ItemVM(definition);
            var loadout = new TransmitterAttackLoadout(new ItemCombatDefinitionResolver());
            loadout.ReplaceItems("special-port", new[] { item });
            var buildService = new TransmitterAttackBuildService(loadout);
            TransmitterAttackInput input = new TransmitterAttackInput(
                1,
                0,
                baseProfile,
                Vector3.zero,
                Vector3.forward,
                10f,
                50f,
                2f,
                1,
                0,
                0f,
                1f,
                "special-port");

            try
            {
                TransmitterAttackBuild build = buildService.BuildAttackParameters(in input);
                Assert.That(build.ProjectileProfile, Is.SameAs(specialProfile));
            }
            finally
            {
                Object.DestroyImmediate(definition);
                Object.DestroyImmediate(combat);
                Object.DestroyImmediate(baseProfile);
                Object.DestroyImmediate(specialProfile);
            }
        }

        [Test]
        public void MultipleSpecialProfilesCombineEffectsAndPreferBoomerangTrajectory()
        {
            ProjectileProfileSO baseProfile = ProjectileProfileSO.CreateRuntimeProfile("base-profile");
            ProjectileProfileSO homingProfile = ProjectileProfileSO.CreateRuntimeProfile("homing-profile");
            ProjectileProfileSO boomerangProfile = ProjectileProfileSO.CreateRuntimeProfile("boomerang-profile");
            HomingTrajectoryDefinitionSO homing = ScriptableObject.CreateInstance<HomingTrajectoryDefinitionSO>();
            BoomerangTrajectoryDefinitionSO boomerang = ScriptableObject.CreateInstance<BoomerangTrajectoryDefinitionSO>();
            ExplodeOnHitEffectDefinitionSO explosion = ScriptableObject.CreateInstance<ExplodeOnHitEffectDefinitionSO>();
            KnockbackEffectDefinitionSO knockback = ScriptableObject.CreateInstance<KnockbackEffectDefinitionSO>();
            ItemCombatDefinitionSO homingCombat = CreateCombatWithProfile(homingProfile);
            ItemCombatDefinitionSO boomerangCombat = CreateCombatWithProfile(boomerangProfile);
            ItemDefinition homingDefinition = ItemDefinition.CreateRuntime(
                "003", "追踪", null, ItemShape.Plus3, homingCombat);
            ItemDefinition boomerangDefinition = ItemDefinition.CreateRuntime(
                "014", "回旋", null, ItemShape.Horizontal2, boomerangCombat);
            ItemVM homingItem = new ItemVM(homingDefinition);
            ItemVM boomerangItem = new ItemVM(boomerangDefinition);
            var loadout = new TransmitterAttackLoadout(new ItemCombatDefinitionResolver());
            loadout.ReplaceItems("combined-port", new[] { homingItem, boomerangItem });
            var buildService = new TransmitterAttackBuildService(loadout);
            ProjectileProfileSO combinedProfile = null;
            TransmitterAttackInput input = new TransmitterAttackInput(
                1,
                0,
                baseProfile,
                Vector3.zero,
                Vector3.forward,
                10f,
                50f,
                2f,
                1,
                0,
                0f,
                1f,
                "combined-port");

            homingProfile.TrajectoryDefinition = homing;
            homingProfile.OnHitEffectDefinitions = new List<ProjectileEffectDefinitionSO> { explosion };
            boomerangProfile.TrajectoryDefinition = boomerang;
            boomerangProfile.OnHitEffectDefinitions = new List<ProjectileEffectDefinitionSO> { knockback };

            try
            {
                TransmitterAttackBuild build = buildService.BuildAttackParameters(in input);
                combinedProfile = build.ProjectileProfile;
                Assert.That(build.ProjectileProfile, Is.Not.SameAs(homingProfile));
                Assert.That(build.ProjectileProfile, Is.Not.SameAs(boomerangProfile));
                Assert.That(build.ProjectileProfile.TrajectoryDefinition, Is.SameAs(boomerang));
                Assert.That(build.ProjectileProfile.OnHitEffectDefinitions, Does.Contain(explosion));
                Assert.That(build.ProjectileProfile.OnHitEffectDefinitions, Does.Contain(knockback));
            }
            finally
            {
                loadout.ClearAllItems();
                Assert.That(combinedProfile == null, Is.True);
                Object.DestroyImmediate(homingDefinition);
                Object.DestroyImmediate(boomerangDefinition);
                Object.DestroyImmediate(homingCombat);
                Object.DestroyImmediate(boomerangCombat);
                Object.DestroyImmediate(baseProfile);
                Object.DestroyImmediate(homingProfile);
                Object.DestroyImmediate(boomerangProfile);
                Object.DestroyImmediate(homing);
                Object.DestroyImmediate(boomerang);
                Object.DestroyImmediate(explosion);
                Object.DestroyImmediate(knockback);
            }
        }

        private static ItemCombatDefinitionSO CreateCombatWithProfile(ProjectileProfileSO profile)
        {
            ItemCombatDefinitionSO combat = ItemCombatDefinitionSO.CreateRuntime(
                CombatScope.Transmitter,
                null);
            SerializedObject serialized = new SerializedObject(combat);
            serialized.FindProperty("transmitterProjectileProfile").objectReferenceValue = profile;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return combat;
        }
    }
}
