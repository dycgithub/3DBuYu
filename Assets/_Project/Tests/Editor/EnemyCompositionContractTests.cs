using System;
using System.Collections.Generic;
using CombatSystem;
using EnemySystem;
using EnemySystem.Components;
using FlockingSystem.ECS;
using Interfaces;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证 Enemy 的组件组合、可选机制和生命周期公开行为。
    /// </summary>
    public sealed class EnemyCompositionContractTests
    {
        private const string EnemyPrefabFolder = "Assets/_Project/Prefabs/Enemy";

        [Test]
        public void EnemyPrefabs_MountCoreMechanismsExplicitly()
        {
            AssertHasComponent("EnemyNormal.prefab", "EnemyHealthComponent");
            AssertHasComponent("EnemyNormal.prefab", "EnemyDamagePresentationComponent");
            AssertHasComponent("EnemyNormal.prefab", "EnemySpatialRegistrationComponent");
        }

        [Test]
        public void EnemyPrefabs_SelectOptionalMechanismsByMounting()
        {
            GameObject normal = LoadPrefab("EnemyNormal.prefab");
            GameObject fast = LoadPrefab("EnemyFast.prefab");
            GameObject tank = LoadPrefab("EnemyTank.prefab");

            Assert.That(normal.GetComponent<EnemyDodgeComponent>(), Is.Null);
            Assert.That(normal.GetComponent<EnemyShieldComponent>(), Is.Null);
            Assert.That(normal.GetComponent<EnemyDeathExplosionComponent>(), Is.Null);

            Assert.That(fast.GetComponent<EnemyDodgeComponent>(), Is.Not.Null);
            Assert.That(fast.GetComponent<EnemyShieldComponent>(), Is.Null);
            Assert.That(fast.GetComponent<EnemyDeathExplosionComponent>(), Is.Null);

            Assert.That(tank.GetComponent<EnemyDodgeComponent>(), Is.Null);
            Assert.That(tank.GetComponent<EnemyShieldComponent>(), Is.Not.Null);
            Assert.That(tank.GetComponent<EnemyDeathExplosionComponent>(), Is.Not.Null);
        }

        [Test]
        public void EnemyWithoutOptionalMechanisms_DoesNotUseConfiguredRules()
        {
            GameObject enemyObject = new("EnemyWithoutOptionalMechanisms");
            EnemyAttributesSO attributes = ScriptableObject.CreateInstance<EnemyAttributesSO>();

            try
            {
                Enemy enemy = enemyObject.AddComponent<Enemy>();
                attributes.baseHealth = 100f;
                attributes.dodgeChance = 1f;
                attributes.shieldValue = 50f;
                attributes.hitEffect = EffectSystem.EffectId.None;
                attributes.deathEffect = EffectSystem.EffectId.None;
                SetEnemyAttributes(enemy, attributes);
                enemy.InitializeForSpawn(1f, 1f);

                DamageResult result = enemy.ReceiveDamageRequest(new DamageRequest
                {
                    DamageAmount = 10f,
                    DamageType = DamageType.Physical
                });

                Assert.That(result.Outcome, Is.EqualTo(DamageOutcome.Applied));
                Assert.That(result.AppliedDamage, Is.EqualTo(10f));
                Assert.That(enemy.CurrentHealth, Is.EqualTo(90f));
                Assert.That(enemyObject.GetComponent<EnemyDodgeComponent>(), Is.Null);
                Assert.That(enemyObject.GetComponent<EnemyShieldComponent>(), Is.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyObject);
                UnityEngine.Object.DestroyImmediate(attributes);
            }
        }

        [Test]
        public void MountedShield_ResetForReuseRestoresFreshState()
        {
            GameObject enemyObject = new("EnemyShieldReuse");
            EnemyAttributesSO attributes = ScriptableObject.CreateInstance<EnemyAttributesSO>();

            try
            {
                enemyObject.SetActive(false);
                Enemy enemy = enemyObject.AddComponent<Enemy>();
                enemyObject.AddComponent<EnemyShieldComponent>();
                attributes.baseHealth = 100f;
                attributes.shieldValue = 10f;
                attributes.hitEffect = EffectSystem.EffectId.None;
                attributes.deathEffect = EffectSystem.EffectId.None;
                SetEnemyAttributes(enemy, attributes);
                enemyObject.SetActive(true);
                enemy.InitializeForSpawn(1f, 1f);

                DamageResult firstHit = ReceivePhysicalDamage(enemy, 5f);
                enemy.ResetForReuse();
                enemy.InitializeForSpawn(1f, 1f);
                DamageResult secondHit = ReceivePhysicalDamage(enemy, 10f);

                Assert.That(firstHit.Outcome, Is.EqualTo(DamageOutcome.Blocked));
                Assert.That(secondHit.Outcome, Is.EqualTo(DamageOutcome.Blocked));
                Assert.That(enemy.CurrentHealth, Is.EqualTo(100f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyObject);
                UnityEngine.Object.DestroyImmediate(attributes);
            }
        }

        [Test]
        public void Enemy_DeathEventsKeepExistingOrder()
        {
            GameObject enemyObject = new("EnemyDeathEventOrder");
            EnemyAttributesSO attributes = ScriptableObject.CreateInstance<EnemyAttributesSO>();
            List<string> events = new();
            Action<Enemy, DamageResult> damageHandler = (_, _) => events.Add("damage");

            try
            {
                Enemy enemy = enemyObject.AddComponent<Enemy>();
                attributes.baseHealth = 10f;
                attributes.hitEffect = EffectSystem.EffectId.None;
                attributes.deathEffect = EffectSystem.EffectId.None;
                SetEnemyAttributes(enemy, attributes);
                enemy.InitializeForSpawn(1f, 1f);
                enemy.OnDied += _ => events.Add("died");
                Enemy.AnyDamageApplied += damageHandler;

                DamageResult result = ReceivePhysicalDamage(enemy, 10f);

                Assert.That(result.WasKilled, Is.True);
                Assert.That(events, Is.EqualTo(new[] { "died", "damage" }));
            }
            finally
            {
                Enemy.AnyDamageApplied -= damageHandler;
                UnityEngine.Object.DestroyImmediate(enemyObject);
                UnityEngine.Object.DestroyImmediate(attributes);
            }
        }

        [Test]
        public void Enemy_StillImplementsExternalContracts()
        {
            Assert.That(typeof(ILockable).IsAssignableFrom(typeof(Enemy)), Is.True);
            Assert.That(typeof(IBuffReceiver).IsAssignableFrom(typeof(Enemy)), Is.True);
            Assert.That(typeof(IDamageRequestReceiver).IsAssignableFrom(typeof(Enemy)), Is.True);
        }

        [Test]
        public void SpatialRegistration_IsAnIndependentMechanism()
        {
            Type componentType = GetComponentType("EnemySpatialRegistrationComponent");

            Assert.That(componentType, Is.Not.Null);
            if (componentType == null)
            {
                return;
            }

            GameObject prefab = LoadPrefab("EnemyNormal.prefab");
            Assert.That(prefab.GetComponent(componentType), Is.Not.Null);
        }

        private static void AssertHasComponent(string fileName, string typeName)
        {
            GameObject prefab = LoadPrefab(fileName);
            Type componentType = GetComponentType(typeName);

            Assert.That(componentType, Is.Not.Null, typeName);
            if (componentType != null)
            {
                Assert.That(prefab.GetComponent(componentType), Is.Not.Null, fileName);
            }
        }

        private static GameObject LoadPrefab(string fileName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{EnemyPrefabFolder}/{fileName}");
            Assert.That(prefab, Is.Not.Null, fileName);
            return prefab;
        }

        private static Type GetComponentType(string typeName)
        {
            return typeof(Enemy).Assembly.GetType($"EnemySystem.Components.{typeName}");
        }

        private static void SetEnemyAttributes(Enemy enemy, EnemyAttributesSO attributes)
        {
            SerializedObject serialized = new(enemy);
            SerializedProperty property = serialized.FindProperty("enemyAttributes");
            Assert.That(property, Is.Not.Null);
            property.objectReferenceValue = attributes;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static DamageResult ReceivePhysicalDamage(Enemy enemy, float amount)
        {
            return enemy.ReceiveDamageRequest(new DamageRequest
            {
                DamageAmount = amount,
                DamageType = DamageType.Physical
            });
        }
    }
}
