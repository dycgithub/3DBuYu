using System.IO;
using CombatSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using NUnit.Framework;

namespace _Project.Tests.Buff
{
    /// <summary>
    /// 锁定 BuffLab 场景的独立结构和必要组件。
    /// </summary>
    public sealed class BuffLabSceneTests
    {
        private const string ScenePath = "Assets/_Project/Tests/Buff/BuffLab.unity";
        private const string DriverPath = "Assets/_Project/Tests/Buff/BuffLabTarget.cs";

        /// <summary>
        /// 验证场景使用 CombatLab 入口并包含真实战斗目标与 Toolkit UI。
        /// </summary>
        [Test]
        public void Scene_ContainsLabParts()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

            try
            {
                GameObject scopeRoot = FindRoot(scene, "CombatLabLifetimeScope");
                GameObject compositionRoot = FindRoot(scene, "CombatLabComposition");
                GameObject cameraRoot = FindRoot(scene, "Camera");
                GameObject centralRoot = FindRoot(scene, "CentralCore");
                GameObject targetRoot = FindRoot(scene, "BuffTarget");
                GameObject driverRoot = FindRoot(scene, "BuffScenario");
                GameObject spatialRoot = FindRoot(scene, "BuffSpatialRegistry");
                GameObject spawnRoot = FindRoot(scene, "BuffSpawnPositionProvider");
                GameObject uiRoot = FindRoot(scene, "BuffLabUI");

                Assert.That(scopeRoot, Is.Not.Null);
                Assert.That(compositionRoot, Is.Not.Null);
                Assert.That(cameraRoot, Is.Not.Null);
                Assert.That(centralRoot, Is.Not.Null);
                Assert.That(targetRoot, Is.Not.Null);
                Assert.That(driverRoot, Is.Not.Null);
                Assert.That(spatialRoot, Is.Not.Null);
                Assert.That(spawnRoot, Is.Not.Null);
                Assert.That(uiRoot, Is.Not.Null);
                Assert.That(scopeRoot.GetComponent<CombatLabLifetimeScope>(), Is.Not.Null);
                Assert.That(compositionRoot.GetComponent<CombatLabComposition>(), Is.Not.Null);
                Assert.That(cameraRoot.GetComponent<Camera>(), Is.Not.Null);
                Assert.That(centralRoot.GetComponent<Play.CentralCore>(), Is.Not.Null);
                EnemySystem.Enemy target = targetRoot.GetComponent<EnemySystem.Enemy>();
                Assert.That(target, Is.Not.Null);
                Assert.That(targetRoot.GetComponent<BuffController>(), Is.Not.Null);
                Assert.That(
                    targetRoot.GetComponent<EnemySystem.Components.EnemyHealthComponent>(),
                    Is.Not.Null);
                Assert.That(driverRoot.GetComponent<BuffLabTarget>(), Is.Not.Null);
                Assert.That(
                    spatialRoot.GetComponent<SpatialSystem.Bridge.SpatialRegistry>(),
                    Is.Not.Null);
                Assert.That(
                    spawnRoot.GetComponent<EnemySystem.Spawning.SpawnPositionProvider>(),
                    Is.Not.Null);
                UIDocument document = uiRoot.GetComponent<UIDocument>();
                Assert.That(document, Is.Not.Null);
                Assert.That(document.visualTreeAsset, Is.Not.Null);
                Assert.That(document.panelSettings, Is.Not.Null);

                BuffLabTarget driver = driverRoot.GetComponent<BuffLabTarget>();
                SerializedObject driverData = new(driver);
                Assert.That(
                    driverData.FindProperty("_target").objectReferenceValue,
                    Is.EqualTo(target));
                BuffDefinitionSO definition =
                    driverData.FindProperty("_buffDefinition").objectReferenceValue as BuffDefinitionSO;
                Assert.That(definition, Is.Not.Null);
                Assert.That(
                    definition.ModifierType,
                    Is.EqualTo(BuffModifierType.MovementSpeedMultiplier));
                Assert.That(definition.DurationSeconds, Is.GreaterThan(0f));

                SerializedObject targetData = new(target);
                Assert.That(
                    targetData.FindProperty("enemyAttributes").objectReferenceValue,
                    Is.Not.Null);

                CombatLabComposition composition = compositionRoot.GetComponent<CombatLabComposition>();
                SerializedObject compositionData = new(composition);
                Assert.That(
                    compositionData.FindProperty("_camera").objectReferenceValue,
                    Is.EqualTo(cameraRoot.GetComponent<Camera>()));
                Assert.That(
                    compositionData.FindProperty("_centralCore").objectReferenceValue,
                    Is.EqualTo(centralRoot.GetComponent<Play.CentralCore>()));
                Assert.That(
                    compositionData.FindProperty("_enemy").objectReferenceValue,
                    Is.EqualTo(targetRoot.GetComponent<EnemySystem.Enemy>()));
                Assert.That(
                    compositionData.FindProperty("_spatialRegistry").objectReferenceValue,
                    Is.EqualTo(spatialRoot.GetComponent<SpatialSystem.Bridge.SpatialRegistry>()));
                Assert.That(
                    compositionData.FindProperty("_spawnPositionProvider").objectReferenceValue,
                    Is.EqualTo(spawnRoot.GetComponent<EnemySystem.Spawning.SpawnPositionProvider>()));

                SerializedProperty modules = compositionData.FindProperty("_modules");
                Assert.That(modules.arraySize, Is.EqualTo(4));
                Assert.That(
                    modules.GetArrayElementAtIndex(0).enumValueIndex,
                    Is.EqualTo((int)CombatLabComposition.CombatLabModuleType.Damage));
                Assert.That(
                    modules.GetArrayElementAtIndex(1).enumValueIndex,
                    Is.EqualTo((int)CombatLabComposition.CombatLabModuleType.Buff));
                Assert.That(
                    modules.GetArrayElementAtIndex(2).enumValueIndex,
                    Is.EqualTo((int)CombatLabComposition.CombatLabModuleType.Spatial));
                Assert.That(
                    modules.GetArrayElementAtIndex(3).enumValueIndex,
                    Is.EqualTo((int)CombatLabComposition.CombatLabModuleType.Enemy));

                CombatLabLifetimeScope scope = scopeRoot.GetComponent<CombatLabLifetimeScope>();
                SerializedObject scopeData = new(scope);
                Assert.That(
                    scopeData.FindProperty("_composition").objectReferenceValue,
                    Is.EqualTo(composition));
                Assert.That(scopeData.FindProperty("_enemyFlockSettings").objectReferenceValue, Is.Not.Null);
                Assert.That(
                    ContainsObject(scopeData.FindProperty("autoInjectGameObjects"), driverRoot),
                    Is.True);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>
        /// 验证场景驱动只通过现有 Buff 与伤害边界完成验证。
        /// </summary>
        [Test]
        public void Driver_UsesBuffBoundaries()
        {
            string source = File.ReadAllText(
                Path.Combine(Application.dataPath, "_Project/Tests/Buff/BuffLabTarget.cs"));

            Assert.That(source, Does.Contain("ApplyBuff"));
            Assert.That(source, Does.Contain("TryApplyDamage"));
            Assert.That(source, Does.Contain("RemoveBySourceId"));
            Assert.That(source, Does.Contain("MaxHealth"));
            Assert.That(source, Does.Contain("MovementSpeedMultiplier"));
            Assert.That(source, Does.Contain("AppliedDamage"));
            Assert.That(source, Does.Not.Contain("DamageTakenMultiplier"));
            Assert.That(source, Does.Contain("DamageOutcome.Invalid"));
            Assert.That(source, Does.Not.Contain("SessionScope"));
        }

        /// <summary>
        /// 验证测试场景不会进入正式构建场景列表。
        /// </summary>
        [Test]
        public void Scene_IsNotAddedToBuildSettings()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int index = 0; index < scenes.Length; index++)
            {
                Assert.That(scenes[index].path, Is.Not.EqualTo(ScenePath));
            }
        }

        /// <summary>
        /// 从指定场景根节点中查找固定名称的对象。
        /// </summary>
        /// <param name="scene">要查询的场景。</param>
        /// <param name="name">根对象名称。</param>
        /// <returns>找到的根对象。</returns>
        private static GameObject FindRoot(Scene scene, string name)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                if (roots[index].name == name)
                {
                    return roots[index];
                }
            }

            return null;
        }

        /// <summary>
        /// 查找序列化对象列表中的场景对象引用。
        /// </summary>
        /// <param name="array">场景对象引用数组。</param>
        /// <param name="target">要查找的对象。</param>
        /// <returns>列表包含目标时返回 true。</returns>
        private static bool ContainsObject(SerializedProperty array, Object target)
        {
            for (int index = 0; index < array.arraySize; index++)
            {
                if (array.GetArrayElementAtIndex(index).objectReferenceValue == target)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
