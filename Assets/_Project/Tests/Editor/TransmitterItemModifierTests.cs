using System.Collections.Generic;
using CombatSystem;
using EnemySystem;
using EnemySystem.Components;
using NUnit.Framework;
using Services;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CombatSystem.Tests
{
    /// <summary>
    /// 验证发射器道具修正的数值、叠加、端口隔离和攻击快照边界。
    /// </summary>
    public sealed class TransmitterItemModifierTests
    {
        private readonly List<Object> _temporaryObjects = new();

        [TearDown]
        public void TearDown()
        {
            for (int index = _temporaryObjects.Count - 1; index >= 0; index--)
            {
                if (_temporaryObjects[index] != null)
                {
                    Object.DestroyImmediate(_temporaryObjects[index]);
                }
            }

            _temporaryObjects.Clear();
        }

        [Test]
        public void DefaultModifiers_AreNeutral()
        {
            TransmitterAttackModifiers modifiers = TransmitterAttackModifiers.Default;

            Assert.That(modifiers.AdditiveDamageBonus, Is.Zero);
            Assert.That(modifiers.DamageMultiplier, Is.EqualTo(1f));
            Assert.That(modifiers.RangeMultiplier, Is.EqualTo(1f));
            Assert.That(modifiers.AttackSpeedMultiplier, Is.EqualTo(1f));
            Assert.That(modifiers.CollisionRadiusMultiplier, Is.EqualTo(1f));
            Assert.That(modifiers.AdditionalProjectileCount, Is.Zero);
            Assert.That(modifiers.AdditionalPenetrationCount, Is.Zero);
            Assert.That(modifiers.DamageTypeOverride.HasValue, Is.False);
        }

        [Test]
        public void NumberedModifiers_ProduceRequestedAttackData()
        {
            ProjectileProfileSO profile = Track(ProjectileProfileSO.CreateRuntimeProfile("modifier-profile"));
            profile.CollisionRadius = 0.25f;
            profile.DamageType = DamageType.Physical;

            AddDamageAttackModifierDefinitionSO damage = Track(
                ScriptableObject.CreateInstance<AddDamageAttackModifierDefinitionSO>());
            damage.DamageAmount = 50f;
            MultiplyCollisionRadiusAttackModifierDefinitionSO collision = Track(
                ScriptableObject.CreateInstance<MultiplyCollisionRadiusAttackModifierDefinitionSO>());
            collision.CollisionRadiusMultiplier = 2f;
            MultiplyAttackSpeedAttackModifierDefinitionSO attackSpeed = Track(
                ScriptableObject.CreateInstance<MultiplyAttackSpeedAttackModifierDefinitionSO>());
            attackSpeed.AttackSpeedMultiplier = 3f;
            SetDamageTypeAttackModifierDefinitionSO damageType = Track(
                ScriptableObject.CreateInstance<SetDamageTypeAttackModifierDefinitionSO>());
            damageType.DamageType = DamageType.Pure;
            AddPenetrationAttackModifierDefinitionSO penetration = Track(
                ScriptableObject.CreateInstance<AddPenetrationAttackModifierDefinitionSO>());
            penetration.AdditionalPenetrationCount = 5;
            AddProjectileCountAttackModifierDefinitionSO projectileCount = Track(
                ScriptableObject.CreateInstance<AddProjectileCountAttackModifierDefinitionSO>());
            projectileCount.AdditionalProjectileCount = 9;

            ItemDefinition item = CreateItem(
                "002",
                damage,
                collision,
                attackSpeed,
                damageType,
                penetration,
                projectileCount);
            var loadout = new TransmitterAttackLoadout(new ItemCombatDefinitionResolver());
            loadout.ReplaceItems("port-a", new[] { new ItemVM(item) });
            var buildService = new TransmitterAttackBuildService(loadout);
            TransmitterAttackInput input = CreateInput("port-a", profile);

            TransmitterAttackBuild build = buildService.BuildAttackParameters(in input);
            var builder = new AttackDataBuilder();

            Assert.That(builder.TryBuildAttackData(in input, in build, out AttackData attack), Is.True);
            Assert.That(attack.DamageAmount, Is.EqualTo(60f));
            Assert.That(attack.CollisionRadius, Is.EqualTo(0.5f));
            Assert.That(attack.DamageType, Is.EqualTo(DamageType.Pure));
            Assert.That(attack.AttacksPerSecond, Is.EqualTo(6f));
            Assert.That(attack.PenetrationCount, Is.EqualTo(5));
            Assert.That(attack.ProjectileCount, Is.EqualTo(10));
        }

        [Test]
        public void MultipleItems_StackEachModifierWithItsExistingSemantics()
        {
            ProjectileProfileSO profile = Track(ProjectileProfileSO.CreateRuntimeProfile("stack-profile"));
            profile.CollisionRadius = 0.1f;

            ItemDefinition damageItem = CreateItem(
                "002",
                CreateModifier<AddDamageAttackModifierDefinitionSO>(modifier =>
                {
                    modifier.DamageAmount = 25f;
                }));
            ItemDefinition secondDamageItem = CreateItem(
                "002-copy",
                CreateModifier<AddDamageAttackModifierDefinitionSO>(modifier =>
                {
                    modifier.DamageAmount = 25f;
                }));
            ItemDefinition collisionItem = CreateItem(
                "004",
                CreateModifier<MultiplyCollisionRadiusAttackModifierDefinitionSO>(modifier =>
                {
                    modifier.CollisionRadiusMultiplier = 2f;
                }));
            ItemDefinition secondCollisionItem = CreateItem(
                "004-copy",
                CreateModifier<MultiplyCollisionRadiusAttackModifierDefinitionSO>(modifier =>
                {
                    modifier.CollisionRadiusMultiplier = 2f;
                }));
            ItemDefinition speedItem = CreateItem(
                "005",
                CreateModifier<MultiplyAttackSpeedAttackModifierDefinitionSO>(modifier =>
                {
                    modifier.AttackSpeedMultiplier = 3f;
                }));
            ItemDefinition secondSpeedItem = CreateItem(
                "005-copy",
                CreateModifier<MultiplyAttackSpeedAttackModifierDefinitionSO>(modifier =>
                {
                    modifier.AttackSpeedMultiplier = 3f;
                }));
            ItemDefinition penetrationItem = CreateItem(
                "008",
                CreateModifier<AddPenetrationAttackModifierDefinitionSO>(modifier =>
                {
                    modifier.AdditionalPenetrationCount = 5;
                }));
            ItemDefinition secondPenetrationItem = CreateItem(
                "008-copy",
                CreateModifier<AddPenetrationAttackModifierDefinitionSO>(modifier =>
                {
                    modifier.AdditionalPenetrationCount = 5;
                }));
            ItemDefinition projectileItem = CreateItem(
                "009",
                CreateModifier<AddProjectileCountAttackModifierDefinitionSO>(modifier =>
                {
                    modifier.AdditionalProjectileCount = 9;
                }));
            ItemDefinition secondProjectileItem = CreateItem(
                "009-copy",
                CreateModifier<AddProjectileCountAttackModifierDefinitionSO>(modifier =>
                {
                    modifier.AdditionalProjectileCount = 9;
                }));

            var loadout = new TransmitterAttackLoadout(new ItemCombatDefinitionResolver());
            loadout.ReplaceItems(
                "port-a",
                new[]
                {
                    new ItemVM(damageItem),
                    new ItemVM(secondDamageItem),
                    new ItemVM(collisionItem),
                    new ItemVM(secondCollisionItem),
                    new ItemVM(speedItem),
                    new ItemVM(secondSpeedItem),
                    new ItemVM(penetrationItem),
                    new ItemVM(secondPenetrationItem),
                    new ItemVM(projectileItem),
                    new ItemVM(secondProjectileItem)
                });
            var buildService = new TransmitterAttackBuildService(loadout);
            TransmitterAttackInput input = CreateInput("port-a", profile);

            TransmitterAttackBuild build = buildService.BuildAttackParameters(in input);

            Assert.That(build.AdditiveDamageBonus, Is.EqualTo(50f));
            Assert.That(build.CollisionRadius, Is.EqualTo(0.4f));
            Assert.That(build.AttacksPerSecond, Is.EqualTo(18f));
            Assert.That(build.PenetrationCount, Is.EqualTo(10));
            Assert.That(build.ProjectileCount, Is.EqualTo(19));
        }

        [Test]
        public void PortId_OnlyUsesTheMatchingPortItems()
        {
            ProjectileProfileSO profile = Track(ProjectileProfileSO.CreateRuntimeProfile("port-isolation-profile"));
            AddDamageAttackModifierDefinitionSO portADamage = Track(
                ScriptableObject.CreateInstance<AddDamageAttackModifierDefinitionSO>());
            portADamage.DamageAmount = 50f;
            AddPenetrationAttackModifierDefinitionSO portBPenetration = Track(
                ScriptableObject.CreateInstance<AddPenetrationAttackModifierDefinitionSO>());
            portBPenetration.AdditionalPenetrationCount = 5;
            ItemDefinition portAItem = CreateItem("002", portADamage);
            ItemDefinition portBItem = CreateItem("008", portBPenetration);

            var loadout = new TransmitterAttackLoadout(new ItemCombatDefinitionResolver());
            loadout.ReplaceItems("port-a", new[] { new ItemVM(portAItem) });
            loadout.ReplaceItems("port-b", new[] { new ItemVM(portBItem) });
            var buildService = new TransmitterAttackBuildService(loadout);
            TransmitterAttackInput inputA = CreateInput("port-a", profile);
            TransmitterAttackInput inputB = CreateInput("port-b", profile);
            TransmitterAttackInput inputC = CreateInput("port-c", profile);

            TransmitterAttackBuild buildA = buildService.BuildAttackParameters(in inputA);
            TransmitterAttackBuild buildB = buildService.BuildAttackParameters(in inputB);
            TransmitterAttackBuild buildC = buildService.BuildAttackParameters(in inputC);

            Assert.That(buildA.AdditiveDamageBonus, Is.EqualTo(50f));
            Assert.That(buildA.PenetrationCount, Is.Zero);
            Assert.That(buildB.AdditiveDamageBonus, Is.Zero);
            Assert.That(buildB.PenetrationCount, Is.EqualTo(5));
            Assert.That(buildC.AdditiveDamageBonus, Is.Zero);
            Assert.That(buildC.PenetrationCount, Is.Zero);
        }

        [Test]
        public void MissingCombatDefinition_DoesNotGiveItem001FallbackDamage()
        {
            ProjectileProfileSO profile = Track(ProjectileProfileSO.CreateRuntimeProfile("fallback-profile"));
            ItemDefinition item = Track(
                ItemDefinition.CreateRuntime("001", "001", null, ItemShape.Single));
            var loadout = new TransmitterAttackLoadout(new ItemCombatDefinitionResolver());
            loadout.ReplaceItems("F/N", new[] { new ItemVM(item) });
            var buildService = new TransmitterAttackBuildService(loadout);
            TransmitterAttackInput input = CreateInput("F/N", profile);

            TransmitterAttackBuild build = buildService.BuildAttackParameters(in input);

            Assert.That(build.AdditiveDamageBonus, Is.Zero);
        }

        [Test]
        public void SharedProjectileProfile_IsNotChangedByAttackModifiers()
        {
            ProjectileProfileSO profile = Track(ProjectileProfileSO.CreateRuntimeProfile("shared-profile"));
            profile.EnergyCost = 3f;
            profile.SpeedUnitsPerSecond = 15f;
            profile.MaximumTravelDistance = 50f;
            profile.CollisionRadius = 0.25f;
            profile.DamageType = DamageType.Physical;

            ItemDefinition item = CreateItem(
                "004",
                CreateModifier<MultiplyCollisionRadiusAttackModifierDefinitionSO>(modifier =>
                {
                    modifier.CollisionRadiusMultiplier = 2f;
                }),
                CreateModifier<SetDamageTypeAttackModifierDefinitionSO>(modifier =>
                {
                    modifier.DamageType = DamageType.Pure;
                }));
            var loadout = new TransmitterAttackLoadout(new ItemCombatDefinitionResolver());
            loadout.ReplaceItems("port-a", new[] { new ItemVM(item) });
            var buildService = new TransmitterAttackBuildService(loadout);
            TransmitterAttackInput input = CreateInput("port-a", profile);
            TransmitterAttackBuild build = buildService.BuildAttackParameters(in input);
            var builder = new AttackDataBuilder();

            Assert.That(builder.TryBuildAttackData(in input, in build, out AttackData attack), Is.True);
            ProjectileData projectile = ProjectileDataFactory.CreateFromAttack(in attack);

            Assert.That(attack.ProjectileProfile, Is.SameAs(profile));
            Assert.That(projectile.ProjectileProfile, Is.SameAs(profile));
            Assert.That(profile.EnergyCost, Is.EqualTo(3f));
            Assert.That(profile.SpeedUnitsPerSecond, Is.EqualTo(15f));
            Assert.That(profile.MaximumTravelDistance, Is.EqualTo(50f));
            Assert.That(profile.CollisionRadius, Is.EqualTo(0.25f));
            Assert.That(profile.DamageType, Is.EqualTo(DamageType.Physical));
        }

        [Test]
        public void AttackSpeedModifier_ChangesTheAttackCooldownInterval()
        {
            ProjectileProfileSO profile = Track(ProjectileProfileSO.CreateRuntimeProfile("cooldown-profile"));
            ItemDefinition item = CreateItem(
                "005",
                CreateModifier<MultiplyAttackSpeedAttackModifierDefinitionSO>(modifier =>
                {
                    modifier.AttackSpeedMultiplier = 3f;
                }));
            var loadout = new TransmitterAttackLoadout(new ItemCombatDefinitionResolver());
            loadout.ReplaceItems("port-a", new[] { new ItemVM(item) });
            var clock = new ManualAttackClock();
            var spawner = new RecordingProjectileSpawner();
            var service = new TransmitterAttackService(
                new AlwaysCombatPhase(),
                new TransmitterAttackBuildService(loadout),
                new AttackDataBuilder(),
                new AttackExecutor(
                    spawner,
                    null,
                    new AlwaysCombatPhase(),
                    new AttackCooldownRegistry(clock)));
            TransmitterAttackInput input = CreateInput("port-a", profile);

            Assert.That(service.TryExecuteAttack(in input), Is.True);
            clock.CurrentTimeSeconds = 0.2f;
            Assert.That(service.TryExecuteAttack(in input), Is.True);
            Assert.That(spawner.BatchCallCount, Is.EqualTo(2));
        }

        [Test]
        public void PureDamageModifier_UsesTheUnifiedShieldContract()
        {
            ProjectileProfileSO profile = Track(ProjectileProfileSO.CreateRuntimeProfile("pure-profile"));
            ItemDefinition item = CreateItem(
                "006",
                CreateModifier<SetDamageTypeAttackModifierDefinitionSO>(modifier =>
                {
                    modifier.DamageType = DamageType.Pure;
                }));
            var loadout = new TransmitterAttackLoadout(new ItemCombatDefinitionResolver());
            loadout.ReplaceItems("port-a", new[] { new ItemVM(item) });
            TransmitterAttackInput input = CreateInput("port-a", profile);
            TransmitterAttackBuild build = new TransmitterAttackBuildService(loadout)
                .BuildAttackParameters(in input);
            var builder = new AttackDataBuilder();
            Assert.That(builder.TryBuildAttackData(in input, in build, out AttackData attack), Is.True);

            Enemy enemy = CreateShieldedEnemy();
            DamageRequest request = new()
            {
                SourceInstanceId = attack.SourceInstanceId,
                AttackInstanceId = attack.AttackInstanceId,
                DamageAmount = 5f,
                DamageType = attack.DamageType,
                HitPosition = Vector3.zero,
                HitNormal = Vector3.up
            };
            var processor = new DamageRequestProcessor();

            Assert.That(processor.TryApplyDamage(enemy, in request, out DamageResult result), Is.True);
            Assert.That(result.AppliedDamage, Is.EqualTo(5f));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(95f));
            Assert.That(enemy.GetComponent<EnemyShieldComponent>().IsShieldActive, Is.True);
        }

        private T CreateModifier<T>(System.Action<T> configure) where T : TransmitterAttackModifierDefinitionSO
        {
            T modifier = Track(ScriptableObject.CreateInstance<T>());
            configure(modifier);
            return modifier;
        }

        private ItemDefinition CreateItem(
            string itemId,
            params TransmitterAttackModifierDefinitionSO[] modifiers)
        {
            ItemCombatDefinitionSO combat = Track(
                ItemCombatDefinitionSO.CreateRuntime(CombatScope.Transmitter, null));
            SerializedObject serialized = new(combat);
            serialized.FindProperty("transmitterAdditiveDamageBonus").floatValue = 0f;
            SerializedProperty definitions = serialized.FindProperty(
                "transmitterAttackModifierDefinitions");
            definitions.arraySize = modifiers.Length;
            for (int index = 0; index < modifiers.Length; index++)
            {
                definitions.GetArrayElementAtIndex(index).objectReferenceValue = modifiers[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return Track(ItemDefinition.CreateRuntime(
                itemId,
                itemId,
                null,
                ItemShape.Single,
                combat));
        }

        private T Track<T>(T temporaryObject) where T : Object
        {
            _temporaryObjects.Add(temporaryObject);
            return temporaryObject;
        }

        private static TransmitterAttackInput CreateInput(
            string portId,
            ProjectileProfileSO profile)
        {
            return new TransmitterAttackInput(
                sourceInstanceId: 1,
                transmitterPortIndex: 0,
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

        private Enemy CreateShieldedEnemy()
        {
            GameObject enemyObject = Track(new GameObject("TransmitterModifierEnemy"));
            Enemy enemy = enemyObject.AddComponent<Enemy>();
            EnemyAttributesSO attributes = Track(ScriptableObject.CreateInstance<EnemyAttributesSO>());
            attributes.baseHealth = 100f;
            attributes.shieldValue = 10f;
            SerializedObject serialized = new(enemy);
            serialized.FindProperty("enemyAttributes").objectReferenceValue = attributes;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            enemy.InitializeForSpawn(1f, 1f);
            enemyObject.AddComponent<EnemyShieldComponent>();
            return enemy;
        }

        private sealed class AlwaysCombatPhase : ICombatPhaseService
        {
            public bool CanPerformCombatActions => true;
        }

        private sealed class ManualAttackClock : IAttackClock
        {
            public float CurrentTimeSeconds { get; set; }
        }

        private sealed class RecordingProjectileSpawner : IProjectileSpawner
        {
            public int BatchCallCount { get; private set; }

            public bool TrySpawnProjectile(in ProjectileData projectileData)
            {
                return TrySpawnProjectileBatch(in projectileData, 1);
            }

            public bool TrySpawnProjectileBatch(
                in ProjectileData projectileData,
                int projectileCount)
            {
                BatchCallCount++;
                return true;
            }
        }
    }
}
