using DG.Tweening;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Play;

namespace _Project.Tests.Juice
{
    /// <summary>
    /// 锁定 Juice 测试场景的参数映射、播放触发和场景隔离契约。
    /// </summary>
    public sealed class JuiceContractTests
    {
        private const string ScenePath = "Assets/_Project/Tests/Juice/JuiceLab.unity";
        private const string CentralPath = "Assets/_Project/SO/CentralBase.asset";
        private const string UxmlPath = "Assets/_Project/Tests/Juice/JuiceControls.uxml";
        private const string PanelSettingsPath = "Assets/UI Toolkit/PanelSettings.asset";

        private GameObject _targetObject;

        /// <summary>
        /// 初始化 DOTween，测试对象由自身生命周期清理 Tween。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            DOTween.Init();
        }

        /// <summary>
        /// 清理测试对象，并由目标组件清理其自身 Tween。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            if (_targetObject != null)
            {
                Object.DestroyImmediate(_targetObject);
            }
        }

        /// <summary>
        /// 验证 UI 滑块值会被限制到 0 到 1，并映射到场景本地的最大 punch 值。
        /// </summary>
        [Test]
        public void MapScalePunch_ClampsSliderAndScalesMaximum()
        {
            Assert.That(JuiceTuning.MapScalePunch(-0.2f, 0.8f), Is.EqualTo(0f));
            Assert.That(JuiceTuning.MapScalePunch(0.5f, 0.8f), Is.EqualTo(0.4f));
            Assert.That(JuiceTuning.MapScalePunch(1.4f, 0.8f), Is.EqualTo(0.8f));
        }

        /// <summary>
        /// 验证 Juice 目标收到触发后会启动 scale punch，而不是只更新调参字段。
        /// </summary>
        [Test]
        public void PlayJuice_StartsScalePunch()
        {
            _targetObject = new GameObject("JuiceTarget");
            JuicePresentationTarget target =
                _targetObject.AddComponent<JuicePresentationTarget>();

            target.PlayJuice();

            Assert.That(target.IsPlaying, Is.True);
        }

        /// <summary>
        /// 验证外部停止该目标的 Tween 后，局部缩放恢复到播放前的值。
        /// </summary>
        [Test]
        public void PlayJuice_RestoresScaleWhenTweenIsKilled()
        {
            _targetObject = new GameObject("JuiceTarget");
            _targetObject.transform.localScale = new Vector3(1.5f, 2f, 2.5f);
            Vector3 initialScale = _targetObject.transform.localScale;
            JuicePresentationTarget target =
                _targetObject.AddComponent<JuicePresentationTarget>();

            target.PlayJuice();
            DOTween.Kill(_targetObject.transform);

            Assert.That(_targetObject.transform.localScale, Is.EqualTo(initialScale));
        }

        /// <summary>
        /// 验证重复播放和禁用目标都不会累积局部缩放偏移。
        /// </summary>
        [Test]
        public void PlayJuice_RestoresScaleAfterReplayAndDisable()
        {
            _targetObject = new GameObject("JuiceTarget");
            _targetObject.transform.localScale = new Vector3(1.25f, 1.5f, 1.75f);
            Vector3 initialScale = _targetObject.transform.localScale;
            JuicePresentationTarget target =
                _targetObject.AddComponent<JuicePresentationTarget>();

            target.PlayJuice();
            target.PlayJuice();
            Assert.That(_targetObject.transform.localScale, Is.EqualTo(initialScale));

            _targetObject.SetActive(false);

            Assert.That(_targetObject.transform.localScale, Is.EqualTo(initialScale));
        }

        /// <summary>
        /// 验证 Juice 场景使用 CombatLab 的显式 Scope、组合和必需表现对象。
        /// </summary>
        [Test]
        public void JuiceScene_ContainsCombatLabCompositionAndTarget()
        {
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Assert.That(sceneAsset, Is.Not.Null, ScenePath);

            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
            Scene scene = default;
            try
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                CombatLabLifetimeScope scope = FindComponent<CombatLabLifetimeScope>(scene);
                CombatLabComposition composition = FindComponent<CombatLabComposition>(scene);
                Camera camera = FindComponent<Camera>(scene);
                CentralCore centralCore = FindComponent<CentralCore>(scene);
                JuicePresentationTarget target = FindComponent<JuicePresentationTarget>(scene);

                Assert.That(scope, Is.Not.Null);
                Assert.That(composition, Is.Not.Null);
                Assert.That(camera, Is.Not.Null);
                Assert.That(centralCore, Is.Not.Null);
                Assert.That(target, Is.Not.Null);
                Assert.That(
                    new SerializedObject(scope).FindProperty("_composition").objectReferenceValue,
                    Is.SameAs(composition));
                Assert.That(
                    ContainsObject(
                        new SerializedObject(scope).FindProperty("autoInjectGameObjects"),
                        target.gameObject),
                    Is.True);
                Assert.That(
                    new SerializedObject(composition).FindProperty("_centralCore").objectReferenceValue,
                    Is.SameAs(centralCore));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }

                RestoreSceneSetup(originalSetup);
            }
        }

        /// <summary>
        /// 验证 Juice 场景引用现有 CentralBase，并使用静态 UI 资源。
        /// </summary>
        [Test]
        public void JuiceScene_UsesCentralBaseAndStaticUiAssets()
        {
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            Assert.That(sceneAsset, Is.Not.Null, ScenePath);

            CentralSO centralConfig = AssetDatabase.LoadAssetAtPath<CentralSO>(CentralPath);
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            PanelSettings panelSettings =
                AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            Assert.That(centralConfig, Is.Not.Null, CentralPath);
            Assert.That(visualTree, Is.Not.Null, UxmlPath);
            Assert.That(panelSettings, Is.Not.Null, PanelSettingsPath);

            SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
            Scene scene = default;
            try
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                CentralCore centralCore = FindComponent<CentralCore>(scene);
                JuicePresentationTarget target = FindComponent<JuicePresentationTarget>(scene);
                UIDocument document = target?.GetComponent<UIDocument>();

                Assert.That(centralCore, Is.Not.Null);
                Assert.That(centralCore.CentralSoConfig, Is.SameAs(centralConfig));
                Assert.That(document, Is.Not.Null);
                Assert.That(document.panelSettings, Is.SameAs(panelSettings));
                Assert.That(document.visualTreeAsset, Is.SameAs(visualTree));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }

                RestoreSceneSetup(originalSetup);
            }
        }

        /// <summary>
        /// 验证 Juice 场景不会被加入项目 Build Settings。
        /// </summary>
        [Test]
        public void JuiceScene_IsNotInBuildSettings()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

            for (int index = 0; index < scenes.Length; index++)
            {
                Assert.That(scenes[index].path, Is.Not.EqualTo(ScenePath));
            }
        }

        /// <summary>
        /// 在场景中查找指定组件，避免测试依赖固定层级路径。
        /// </summary>
        /// <typeparam name="T">要查找的组件类型。</typeparam>
        /// <param name="scene">要检查的场景。</param>
        /// <returns>场景中的第一个指定组件。</returns>
        private static T FindComponent<T>(Scene scene) where T : Component
        {
            GameObject[] roots = scene.GetRootGameObjects();

            for (int index = 0; index < roots.Length; index++)
            {
                T component = roots[index].GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        /// <summary>
        /// 恢复测试打开场景前的 Editor 场景布局。
        /// </summary>
        /// <param name="setup">测试前的场景布局。</param>
        private static void RestoreSceneSetup(SceneSetup[] setup)
        {
            if (setup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
        }

        /// <summary>
        /// 判断 Scope 自动注入数组是否包含指定场景对象。
        /// </summary>
        /// <param name="array">Scope 自动注入对象数组。</param>
        /// <param name="target">要查找的场景对象。</param>
        /// <returns>数组包含目标时返回 true。</returns>
        private static bool ContainsObject(SerializedProperty array, Object target)
        {
            if (array == null)
            {
                return false;
            }

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
