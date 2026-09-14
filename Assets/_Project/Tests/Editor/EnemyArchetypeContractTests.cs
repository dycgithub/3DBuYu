using System;
using System.Collections.Generic;
using CombatSystem;
using EnemySystem;
using EnemySystem.Components;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证 001～012 的静态配置、预制体组合和统一占位模型契约。
    /// </summary>
    public sealed class EnemyArchetypeContractTests
    {
        private const string EnemyPrefabFolder = "Assets/_Project/Prefabs/Enemy";
        private const string EnemyAssetFolder = "Assets/_Project/SO/Enemy";
        private const string PlaceholderPrefabPath =
            "Assets/_Project/Prefabs/Enemy/EnemyPlaceholderModel.prefab";

        private static readonly string[] EnemyPrefabNames =
        {
            "EnemyNormal.prefab",
            "EnemyFast.prefab",
            "EnemyTank.prefab",
            "EnemyAssault.prefab",
            "EnemyGrowth.prefab",
            "EnemyReflect.prefab",
            "EnemyExploder.prefab",
            "EnemyShooter.prefab",
            "EnemySlow.prefab",
            "EnemyHealer.prefab",
            "EnemyStrongHealer.prefab",
            "EnemyMother.prefab",
            "EnemyNormalChild.prefab"
        };

        [Test]
        public void EnemyType_PreservesExistingValuesAndAddsRequestedTypes()
        {
            Assert.That((int)EnemyType.Normal, Is.EqualTo(0));
            Assert.That((int)EnemyType.Fast, Is.EqualTo(1));
            Assert.That((int)EnemyType.Tank, Is.EqualTo(2));
            Assert.That(Enum.GetNames(typeof(EnemyType)), Does.Contain("Assault"));
            Assert.That(Enum.GetNames(typeof(EnemyType)), Does.Contain("Growth"));
            Assert.That(Enum.GetNames(typeof(EnemyType)), Does.Contain("Reflect"));
            Assert.That(Enum.GetNames(typeof(EnemyType)), Does.Contain("Exploder"));
            Assert.That(Enum.GetNames(typeof(EnemyType)), Does.Contain("Shooter"));
            Assert.That(Enum.GetNames(typeof(EnemyType)), Does.Contain("Slow"));
            Assert.That(Enum.GetNames(typeof(EnemyType)), Does.Contain("Healer"));
            Assert.That(Enum.GetNames(typeof(EnemyType)), Does.Contain("StrongHealer"));
            Assert.That(Enum.GetNames(typeof(EnemyType)), Does.Contain("Mother"));
        }

        [Test]
        public void EnemyPrefabs_ContainCoreComponentsAndOneBehavior()
        {
            Type[] coreTypes =
            {
                typeof(Enemy),
                typeof(EnemyHealthComponent),
                typeof(EnemyDamagePresentationComponent),
                typeof(EnemySpatialRegistrationComponent),
                typeof(EnemyDeathRewardComponent),
                typeof(BuffController)
            };

            for (int index = 0; index < EnemyPrefabNames.Length - 1; index++)
            {
                GameObject prefab = LoadPrefab(EnemyPrefabNames[index]);
                for (int coreIndex = 0; coreIndex < coreTypes.Length; coreIndex++)
                {
                    Assert.That(
                        prefab.GetComponent(coreTypes[coreIndex]),
                        Is.Not.Null,
                        $"{EnemyPrefabNames[index]} 缺少 {coreTypes[coreIndex].Name}");
                }
            }
        }

        [Test]
        public void NewBehaviorPrefabs_MountExpectedComponents()
        {
            AssertHasComponent<EnemyAssaultComponent>("EnemyAssault.prefab");
            AssertHasComponent<EnemyGrowthComponent>("EnemyGrowth.prefab");
            AssertHasComponent<EnemyReflectComponent>("EnemyReflect.prefab");
            AssertHasComponent<EnemyDeathExplosionComponent>("EnemyExploder.prefab");
            AssertHasComponent<EnemyShooterComponent>("EnemyShooter.prefab");
            AssertHasComponent<EnemyAreaEffectComponent>("EnemySlow.prefab");
            AssertHasComponent<EnemyAreaEffectComponent>("EnemyHealer.prefab");
            AssertHasComponent<EnemyAreaEffectComponent>("EnemyStrongHealer.prefab");
            AssertHasComponent<EnemyMotherComponent>("EnemyMother.prefab");
        }

        [Test]
        public void EnemyPrefabs_UseTheSamePlaceholderMesh()
        {
            GameObject placeholder = AssetDatabase.LoadAssetAtPath<GameObject>(PlaceholderPrefabPath);
            Assert.That(placeholder, Is.Not.Null);

            MeshFilter placeholderMesh = placeholder.GetComponentInChildren<MeshFilter>(true);
            Assert.That(placeholderMesh, Is.Not.Null);
            Assert.That(placeholderMesh.sharedMesh, Is.Not.Null);

            for (int index = 0; index < EnemyPrefabNames.Length; index++)
            {
                GameObject prefab = LoadPrefab(EnemyPrefabNames[index]);
                MeshFilter meshFilter = prefab.GetComponentInChildren<MeshFilter>(true);
                Assert.That(meshFilter, Is.Not.Null, EnemyPrefabNames[index]);
                Assert.That(meshFilter.sharedMesh, Is.SameAs(placeholderMesh.sharedMesh));
            }
        }

        [Test]
        public void EnemyAttributes_ExposeAllRequestedIds()
        {
            string[] assetNames =
            {
                "EnemyAttributes_Normal.asset",
                "EnemyAttributes_Fast.asset",
                "EnemyAttributes_Tank.asset",
                "EnemyAttributes_Assault.asset",
                "EnemyAttributes_Growth.asset",
                "EnemyAttributes_Reflect.asset",
                "EnemyAttributes_Exploder.asset",
                "EnemyAttributes_Shooter.asset",
                "EnemyAttributes_Slow.asset",
                "EnemyAttributes_Healer.asset",
                "EnemyAttributes_StrongHealer.asset",
                "EnemyAttributes_Mother.asset"
            };

            for (int index = 0; index < assetNames.Length; index++)
            {
                EnemyAttributesSO attributes = AssetDatabase.LoadAssetAtPath<EnemyAttributesSO>(
                    $"{EnemyAssetFolder}/{assetNames[index]}");
                Assert.That(attributes, Is.Not.Null, assetNames[index]);
                Assert.That(attributes.enemyId, Is.EqualTo(index + 1), assetNames[index]);
            }
        }

        private static GameObject LoadPrefab(string fileName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{EnemyPrefabFolder}/{fileName}");
            Assert.That(prefab, Is.Not.Null, fileName);
            return prefab;
        }

        private static void AssertHasComponent<T>(string fileName)
            where T : Component
        {
            GameObject prefab = LoadPrefab(fileName);
            Assert.That(prefab.GetComponent<T>(), Is.Not.Null, fileName);
        }
    }
}
