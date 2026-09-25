using System;
using System.IO;
using EnemySystem;
using GameSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证静态玩法配置由 SO 提供，玩家存档类型不保留已证实的废弃入口。
    /// </summary>
    public sealed class ConfigurationSourceGovernanceTests
    {
        private const string StageConfigPath = "Assets/_Project/SO/GamePlaySettings/StageConfig.asset";
        private const string NormalDifficultyPath =
            "Assets/_Project/SO/GamePlaySettings/Difficulties/Normal.asset";
        private const string EnemyAttributesFolder = "Assets/_Project/SO/Enemy";

        [Test]
        public void StageConfig_PrefersConfiguredDefaultDifficulty()
        {
            StageConfig stage = AssetDatabase.LoadAssetAtPath<StageConfig>(StageConfigPath);
            Assert.That(stage, Is.Not.Null);
            Assert.That(stage.defaultDifficulty, Is.Not.Null);

            DifficultySelection selection = new(
                stage.defaultDifficulty,
                stage.AvailableDifficulties);

            Assert.That(selection.Current, Is.SameAs(stage.defaultDifficulty));
        }

        [Test]
        public void GameplayConfigAssets_OverrideDefaultsUsedForNewAssets()
        {
            StageConfig stage = AssetDatabase.LoadAssetAtPath<StageConfig>(StageConfigPath);
            DifficultyConfig normal = AssetDatabase.LoadAssetAtPath<DifficultyConfig>(NormalDifficultyPath);
            StageConfig newStage = ScriptableObject.CreateInstance<StageConfig>();
            DifficultyConfig newDifficulty = ScriptableObject.CreateInstance<DifficultyConfig>();

            try
            {
                Assert.That(stage.initialEnergy, Is.Not.EqualTo(newStage.initialEnergy));
                Assert.That(normal.NormalPopulationTarget, Is.Not.EqualTo(newDifficulty.NormalPopulationTarget));
                Assert.That(stage.initialEnergy, Is.EqualTo(500f));
                Assert.That(normal.NormalPopulationTarget, Is.EqualTo(800));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(newStage);
                UnityEngine.Object.DestroyImmediate(newDifficulty);
            }
        }

        [Test]
        public void EnemyAttributesAssets_DoNotContainRemovedAreaHealingField()
        {
            string[] guids = AssetDatabase.FindAssets("t:EnemyAttributesSO", new[] { EnemyAttributesFolder });
            Assert.That(guids, Is.Not.Empty);

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                EnemyAttributesSO attributes = AssetDatabase.LoadAssetAtPath<EnemyAttributesSO>(assetPath);
                Assert.That(attributes, Is.Not.Null, assetPath);
                Assert.That(
                    new SerializedObject(attributes).FindProperty("areaHealPerSecond"),
                    Is.Null,
                    assetPath);

                string absolutePath = Path.Combine(
                    Directory.GetParent(Application.dataPath).FullName,
                    assetPath.Replace('/', Path.DirectorySeparatorChar));
                Assert.That(File.ReadAllText(absolutePath), Does.Not.Contain("areaHealPerSecond:"), assetPath);
            }
        }

        [Test]
        public void GameSaveData_IsAbsentWhenNoReferencesRemain()
        {
            Type saveDataType = typeof(SaveSystem).Assembly.GetType("GameSystem.GameSaveData");

            Assert.That(saveDataType, Is.Null);
            Assert.That(typeof(SaveSystem).GetMethod("SaveGameData"), Is.Null);
            Assert.That(typeof(SaveSystem).GetMethod("LoadGameData"), Is.Null);
        }
    }
}
