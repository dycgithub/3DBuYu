using CombatSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace CombatSystem.Tests
{
    /// <summary>
    /// 验证发射器攻击修正按 PortId 隔离，并只在对应背包内容变化后重建。
    /// </summary>
    public sealed class TransmitterAttackCachingTests
    {
        [Test]
        public void BuildUsesOnlyRequestedPort()
        {
            ProjectileProfileSO profile = ProjectileProfileSO.CreateRuntimeProfile("port-id-test");
            ItemFixture portA = CreateItem(3f);
            ItemFixture portB = CreateItem(7f);
            var loadout = new TransmitterAttackLoadout(new ItemCombatDefinitionResolver());
            loadout.ReplaceItems("port-a", new[] { new ItemVM(portA.Definition) });
            loadout.ReplaceItems("port-b", new[] { new ItemVM(portB.Definition) });
            var buildService = new TransmitterAttackBuildService(loadout);

            try
            {
                TransmitterAttackInput inputA = CreateInput("port-a", 8, profile);
                TransmitterAttackBuild buildA = buildService.BuildAttackParameters(in inputA);

                Assert.That(buildA.AdditiveDamageBonus, Is.EqualTo(3f));
                Assert.That(portA.Modifier.ApplyCount, Is.EqualTo(1));
                Assert.That(portB.Modifier.ApplyCount, Is.Zero);

                TransmitterAttackInput inputB = CreateInput("port-b", 1, profile);
                TransmitterAttackBuild buildB = buildService.BuildAttackParameters(in inputB);

                Assert.That(buildB.AdditiveDamageBonus, Is.EqualTo(7f));
                Assert.That(portB.Modifier.ApplyCount, Is.EqualTo(1));
            }
            finally
            {
                DestroyFixture(portA);
                DestroyFixture(portB);
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ContentChangeRebuildsOnlyChangedPort()
        {
            ProjectileProfileSO profile = ProjectileProfileSO.CreateRuntimeProfile("cache-test");
            ItemFixture initialA = CreateItem(3f);
            ItemFixture changedA = CreateItem(11f);
            ItemFixture portB = CreateItem(7f);
            var loadout = new TransmitterAttackLoadout(new ItemCombatDefinitionResolver());
            loadout.ReplaceItems("port-a", new[] { new ItemVM(initialA.Definition) });
            loadout.ReplaceItems("port-b", new[] { new ItemVM(portB.Definition) });
            var buildService = new TransmitterAttackBuildService(loadout);

            try
            {
                TransmitterAttackInput inputA = CreateInput("port-a", 8, profile);
                TransmitterAttackInput inputB = CreateInput("port-b", 1, profile);

                buildService.BuildAttackParameters(in inputA);
                buildService.BuildAttackParameters(in inputB);
                buildService.BuildAttackParameters(in inputA);
                buildService.BuildAttackParameters(in inputB);

                Assert.That(initialA.Modifier.ApplyCount, Is.EqualTo(1));
                Assert.That(portB.Modifier.ApplyCount, Is.EqualTo(1));

                loadout.ReplaceItems("port-a", new[] { new ItemVM(changedA.Definition) });

                TransmitterAttackBuild changedBuild = buildService.BuildAttackParameters(in inputA);
                TransmitterAttackBuild unchangedBuild = buildService.BuildAttackParameters(in inputB);

                Assert.That(changedBuild.AdditiveDamageBonus, Is.EqualTo(11f));
                Assert.That(unchangedBuild.AdditiveDamageBonus, Is.EqualTo(7f));
                Assert.That(initialA.Modifier.ApplyCount, Is.EqualTo(1));
                Assert.That(changedA.Modifier.ApplyCount, Is.EqualTo(1));
                Assert.That(portB.Modifier.ApplyCount, Is.EqualTo(1));
            }
            finally
            {
                DestroyFixture(initialA);
                DestroyFixture(changedA);
                DestroyFixture(portB);
                Object.DestroyImmediate(profile);
            }
        }

        private static TransmitterAttackInput CreateInput(
            string portId,
            int portIndex,
            ProjectileProfileSO profile)
        {
            return new TransmitterAttackInput(
                sourceInstanceId: 1,
                transmitterPortIndex: portIndex,
                projectileProfile: profile,
                originPosition: Vector3.zero,
                launchDirection: Vector3.forward,
                baseDamage: 10f,
                baseRange: 50f,
                attacksPerSecond: 2f,
                requestedProjectileCount: 1,
                requestedPenetrationCount: 0,
                criticalHitChance: 0f,
                criticalHitDamageMultiplier: 1f,
                portId: portId);
        }

        private static ItemFixture CreateItem(float bonus)
        {
            var modifier = ScriptableObject.CreateInstance<CountingModifierDefinitionSO>();
            modifier.DamageBonus = bonus;

            ItemCombatDefinitionSO combat = ItemCombatDefinitionSO.CreateRuntime(
                CombatScope.Transmitter,
                null);
            SerializedObject serialized = new SerializedObject(combat);
            serialized.FindProperty("transmitterAdditiveDamageBonus").floatValue = 0f;
            SerializedProperty definitions = serialized.FindProperty("transmitterAttackModifierDefinitions");
            definitions.arraySize = 1;
            definitions.GetArrayElementAtIndex(0).objectReferenceValue = modifier;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            ItemDefinition definition = ItemDefinition.CreateRuntime(
                "test-item",
                "测试物品",
                null,
                ItemShape.Single,
                combat);
            return new ItemFixture(definition, combat, modifier);
        }

        private static void DestroyFixture(ItemFixture fixture)
        {
            Object.DestroyImmediate(fixture.Definition);
            Object.DestroyImmediate(fixture.Combat);
            Object.DestroyImmediate(fixture.Modifier);
        }

        private sealed class ItemFixture
        {
            public ItemDefinition Definition { get; }
            public ItemCombatDefinitionSO Combat { get; }
            public CountingModifierDefinitionSO Modifier { get; }

            public ItemFixture(
                ItemDefinition definition,
                ItemCombatDefinitionSO combat,
                CountingModifierDefinitionSO modifier)
            {
                Definition = definition;
                Combat = combat;
                Modifier = modifier;
            }
        }

        private sealed class CountingModifierDefinitionSO : TransmitterAttackModifierDefinitionSO
        {
            public float DamageBonus;
            public int ApplyCount { get; private set; }

            public override void Apply(ref TransmitterAttackModifiers modifiers)
            {
                ApplyCount++;
                modifiers.AdditiveDamageBonus += DamageBonus;
            }
        }
    }
}
