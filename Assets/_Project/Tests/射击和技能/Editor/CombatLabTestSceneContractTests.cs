using System.IO;
using CombatSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace CombatLab.Tests.Editor
{
    /// <summary>
    /// 锁定射击和技能测试场景的组成、注入入口与独立性。
    /// </summary>
    public sealed class CombatLabTestSceneContractTests
    {
        private const string ScenePath =
            "Assets/_Project/Tests/射击和技能/CombatLabTestScene.unity";
        private const string DriverPath =
            "Assets/_Project/Tests/射击和技能/CombatLabTestDriver.cs";

        /// <summary>测试场景包含 CombatLab 作用域及最小演示对象。</summary>
        [Test]
        public void Scene_ContainsCombatLabDemoObjects()
        {
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Assert.That(sceneAsset, Is.Not.Null, ScenePath);

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                Assert.That(FindRoot(scene, "CombatLabLifetimeScope"), Is.Not.Null);
                Assert.That(FindRoot(scene, "CombatLabComposition"), Is.Not.Null);
                Assert.That(FindRoot(scene, "Camera"), Is.Not.Null);
                Assert.That(FindRoot(scene, "CentralCore"), Is.Not.Null);
                Assert.That(FindRoot(scene, "CombatLabTestDriver"), Is.Not.Null);
                Assert.That(FindRoot(scene, "CombatLabTestTarget"), Is.Not.Null);
                Assert.That(FindRoot(scene, "CombatLabTestUI"), Is.Not.Null);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>场景作用域显式引用组合，并通过自动注入列表注入场景驱动。</summary>
        [Test]
        public void Scene_UsesExplicitCompositionAndInjection()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                GameObject scopeObject = FindRoot(scene, "CombatLabLifetimeScope");
                GameObject compositionObject = FindRoot(scene, "CombatLabComposition");
                GameObject driverObject = FindRoot(scene, "CombatLabTestDriver");
                Assert.That(scopeObject, Is.Not.Null);
                Assert.That(compositionObject, Is.Not.Null);
                Assert.That(driverObject, Is.Not.Null);

                SerializedObject scope = new(scopeObject.GetComponent<CombatLabLifetimeScope>());
                Assert.That(
                    scope.FindProperty("_composition").objectReferenceValue,
                    Is.SameAs(compositionObject.GetComponent<CombatLabComposition>()));
                Assert.That(
                    scope.FindProperty("_combatEffectCatalog").objectReferenceValue,
                    Is.Not.Null);
                Assert.That(
                    scope.FindProperty("_enemyFlockSettings").objectReferenceValue,
                    Is.Not.Null);

                SerializedProperty injectedObjects = scope.FindProperty("autoInjectGameObjects");
                Assert.That(injectedObjects, Is.Not.Null);
                Assert.That(ContainsObject(injectedObjects, driverObject), Is.True);
                Assert.That(ContainsObject(injectedObjects, FindRoot(scene, "CombatLabTestTarget")), Is.True);

                SerializedObject composition =
                    new(compositionObject.GetComponent<CombatLabComposition>());
                Assert.That(composition.FindProperty("_camera").objectReferenceValue, Is.Not.Null);
                Assert.That(
                    composition.FindProperty("_centralCore").objectReferenceValue,
                    Is.SameAs(FindRoot(scene, "CentralCore").GetComponent<Play.CentralCore>()));
                Assert.That(
                    composition.FindProperty("_enemy").objectReferenceValue,
                    Is.SameAs(FindRoot(scene, "CombatLabTestTarget").GetComponent<EnemySystem.Enemy>()));
                Assert.That(
                    composition.FindProperty("_spawnPositionProvider").objectReferenceValue,
                    Is.Not.Null);
                Assert.That(
                    composition.FindProperty("_inputService").objectReferenceValue,
                    Is.Null);
                Assert.That(
                    composition.FindProperty("_transmitterFireController").objectReferenceValue,
                    Is.Null);
                Assert.That(
                    FindRoot(scene, "CentralCore").GetComponent<Play.TransmitterFireController>(),
                    Is.Null);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>场景中的 UIDocument 明确绑定 PanelSettings 和 UXML。</summary>
        [Test]
        public void Scene_HasValidUiReferences()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                GameObject uiObject = FindRoot(scene, "CombatLabTestUI");
                Assert.That(uiObject, Is.Not.Null);

                UIDocument document = uiObject.GetComponent<UIDocument>();
                Assert.That(document, Is.Not.Null);
                Assert.That(document.panelSettings, Is.Not.Null);
                Assert.That(document.visualTreeAsset, Is.Not.Null);
                Assert.That(
                    AssetDatabase.GetAssetPath(document.panelSettings),
                    Is.EqualTo("Assets/UI Toolkit/PanelSettings.asset"));
                Assert.That(
                    AssetDatabase.GetAssetPath(document.visualTreeAsset),
                    Is.EqualTo("Assets/_Project/Tests/射击和技能/CombatLabTestScene.uxml"));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>场景中的正式配置和目标 Prefab 均保持有效引用。</summary>
        [Test]
        public void Scene_HasValidCombatReferences()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                GameObject scopeObject = FindRoot(scene, "CombatLabLifetimeScope");
                GameObject centralObject = FindRoot(scene, "CentralCore");
                GameObject driverObject = FindRoot(scene, "CombatLabTestDriver");
                GameObject targetObject = FindRoot(scene, "CombatLabTestTarget");

                Assert.That(
                    scopeObject.GetComponent<CombatLabLifetimeScope>(),
                    Is.Not.Null);
                Assert.That(
                    centralObject.GetComponent<Play.CentralCore>(),
                    Is.Not.Null);
                Assert.That(
                    driverObject.GetComponent<CombatLabTestDriver>(),
                    Is.Not.Null);
                Assert.That(targetObject.GetComponent<EnemySystem.Enemy>(), Is.Not.Null);
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<Play.CentralSO>(
                        "Assets/_Project/SO/CentralBase.asset"),
                    Is.Not.Null);
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<EffectSystem.CombatEffectCatalogSO>(
                        "Assets/_Project/SO/Effects/CombatEffectCatalog.asset"),
                    Is.Not.Null);
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<SkillDefinitionSO>(
                        "Assets/_Project/SO/Item/Central/SkillDefinition_010.asset"),
                    Is.Not.Null);
                Assert.That(
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/_Project/Prefabs/Enemy/EnemyNormal.prefab"),
                    Is.Not.Null);

                SerializedObject central =
                    new(centralObject.GetComponent<Play.CentralCore>());
                Assert.That(central.FindProperty("centralSo").objectReferenceValue, Is.Not.Null);

                SerializedObject scope =
                    new(scopeObject.GetComponent<CombatLabLifetimeScope>());
                Assert.That(
                    scope.FindProperty("_combatEffectCatalog").objectReferenceValue,
                    Is.Not.Null);

                SerializedObject driver =
                    new(driverObject.GetComponent<CombatLabTestDriver>());
                Assert.That(driver.FindProperty("_skillDefinition").objectReferenceValue, Is.Not.Null);

                Object source = PrefabUtility.GetCorrespondingObjectFromSource(targetObject);
                Assert.That(source, Is.Not.Null);
                Assert.That(
                    AssetDatabase.GetAssetPath(source),
                    Is.EqualTo("Assets/_Project/Prefabs/Enemy/EnemyNormal.prefab"));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        /// <summary>测试场景不进入正式 Build Settings。</summary>
        [Test]
        public void Scene_IsNotAddedToBuildSettings()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int index = 0; index < scenes.Length; index++)
            {
                Assert.That(scenes[index].path, Is.Not.EqualTo(ScenePath));
            }
        }

        /// <summary>Builder 不应切走用户场景，也不应保存用户的其他场景或资产。</summary>
        [Test]
        public void Builder_UsesSafeAdditiveSaveFlow()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Tests/射击和技能/Editor/CombatLabTestSceneBuilder.cs");
            string source = File.ReadAllText(path);

            Assert.That(source, Does.Contain("NewSceneMode.Additive"));
            Assert.That(source, Does.Contain("SceneManager.GetActiveScene"));
            Assert.That(source, Does.Contain("SceneManager.SetActiveScene"));
            Assert.That(source, Does.Contain("EditorSceneManager.CloseScene"));
            Assert.That(source, Does.Contain("try"));
            Assert.That(source, Does.Contain("finally"));
            Assert.That(source, Does.Not.Contain("NewSceneMode.Single"));
            Assert.That(source, Does.Not.Contain("SaveAssets"));
            Assert.That(source, Does.Not.Contain("SaveOpenScenes"));
            Assert.That(source, Does.Not.Contain("SaveCurrentModifiedScenesIfUserWantsTo"));
        }

        /// <summary>Builder 不创建输入控制器或伪造 InputService 来掩盖缺失依赖。</summary>
        [Test]
        public void Builder_DoesNotCreateInputDrivenFireController()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Tests/射击和技能/Editor/CombatLabTestSceneBuilder.cs");
            string source = File.ReadAllText(path);

            Assert.That(source, Does.Not.Contain("TransmitterFireController"));
            Assert.That(source, Does.Not.Contain("InputService"));
        }

        /// <summary>驱动必须直接调用既有射击和技能边界，不复制完整游戏流程。</summary>
        [Test]
        public void Driver_UsesDirectCombatBoundaries()
        {
            string path = Path.Combine(Application.dataPath, "_Project/Tests/射击和技能/CombatLabTestDriver.cs");
            string source = File.ReadAllText(path);

            Assert.That(source, Does.Contain("ITransmitterAttackService"));
            Assert.That(source, Does.Contain("ISkillExecutionService"));
            Assert.That(source, Does.Contain("TryExecuteAttack"));
            Assert.That(source, Does.Contain("SkillExecutionRequest"));
            Assert.That(source, Does.Not.Contain("SessionScope"));
            Assert.That(source, Does.Not.Contain("GameScene"));
            Assert.That(source, Does.Not.Contain("FindObjectsOfType"));
            Assert.That(source, Does.Not.Contain("FindObjectOfType"));
            Assert.That(source, Does.Not.Contain("RegisterLazy"));
            Assert.That(source, Does.Contain("SkillTargetOption"));
            Assert.That(source, Does.Contain("Array.Empty<IDamageable>()"));
            Assert.That(source, Does.Contain("ConfiguredTarget"));
            Assert.That(source, Does.Contain("NoTarget"));
            Assert.That(source, Does.Contain("ActionFailed"));
            Assert.That(source, Does.Contain("射击失败"));
            Assert.That(source, Does.Contain("技能失败"));
            Assert.That(source, Does.Contain("仅用于测试"));
        }

        /// <summary>驱动方法名称短且与返回语义一致，避免隐藏失败结果。</summary>
        [Test]
        public void Driver_UsesClearShortMethodNames()
        {
            string path = Path.Combine(
                Application.dataPath,
                "_Project/Tests/射击和技能/CombatLabTestDriver.cs");
            string source = File.ReadAllText(path);

            Assert.That(source, Does.Contain("public bool TryFireOnce()"));
            Assert.That(source, Does.Contain("public SkillExecutionResult ExecuteSkillTest("));
            Assert.That(source, Does.Contain("public SkillExecutionResult ExecuteSkillNoDefinition()"));
            Assert.That(source, Does.Not.Contain("TryExecuteSkill("));
            Assert.That(source, Does.Not.Contain("TryExecuteWithoutDefinition("));
        }

        /// <summary>技能执行器在没有目标或没有定义时返回失败，不抛出异常。</summary>
        [Test]
        public void SkillExecution_InvalidInputReturnsFailureWithoutThrowing()
        {
            SkillDefinitionSO definition = SkillDefinitionSO.CreateRuntimeDefinition(
                "combat-lab-contract",
                10f);
            SkillExecutor executor = new(null, new AttackInstanceIdGenerator());

            try
            {
                SkillExecutionResult noTargetResult = default;
                Assert.DoesNotThrow(() => noTargetResult = executor.Execute(
                    new SkillExecutionRequest(
                        definition,
                        1,
                        Vector3.zero,
                        System.Array.Empty<Interfaces.IDamageable>())));
                Assert.That(noTargetResult.Succeeded, Is.False);
                Assert.That(
                    noTargetResult.FailureReason,
                    Is.EqualTo(SkillExecutionFailureReason.NoValidTarget));

                SkillExecutionResult noDefinitionResult = default;
                Assert.DoesNotThrow(() => noDefinitionResult = executor.Execute(
                    new SkillExecutionRequest(
                        null,
                        1,
                        Vector3.zero,
                        System.Array.Empty<Interfaces.IDamageable>())));
                Assert.That(noDefinitionResult.Succeeded, Is.False);
                Assert.That(
                    noDefinitionResult.FailureReason,
                    Is.EqualTo(SkillExecutionFailureReason.SkillNotFound));
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        private static GameObject FindRoot(Scene scene, string objectName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                if (roots[index].name == objectName)
                {
                    return roots[index];
                }
            }

            return null;
        }

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
