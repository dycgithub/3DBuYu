using System.Linq;
using GameSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 验证全局根容器、场景加载器和 UI 子容器的资源层级契约。
/// </summary>
public sealed class ProjectScopeContractTests
{
    private const string SettingsPath = "Assets/_Project/SO/VContainerSettings.asset";
    private const string ScopePrefabPath = "Assets/_Project/Prefabs/ScopeContainer.prefab";
    private const string UIScenePath = "Assets/_Project/Scenes/UIScene.unity";

    [Test]
    public void VContainerSettings_UsesScopeContainerPrefab()
    {
        ScriptableObject settings = AssetDatabase.LoadAssetAtPath<ScriptableObject>(SettingsPath);
        GameObject scopePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScopePrefabPath);

        Assert.That(settings, Is.Not.Null, SettingsPath);
        Assert.That(scopePrefab, Is.Not.Null, ScopePrefabPath);

        SerializedProperty rootLifetimeScope = new SerializedObject(settings).FindProperty("RootLifetimeScope");

        Assert.That(rootLifetimeScope, Is.Not.Null);
        Assert.That(rootLifetimeScope.objectReferenceValue, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(rootLifetimeScope.objectReferenceValue), Is.EqualTo(ScopePrefabPath));
    }

    [Test]
    public void ScopeContainerPrefab_ContainsOneProjectScopeAndOneSceneLoader()
    {
        GameObject scopePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ScopePrefabPath);

        Assert.That(scopePrefab, Is.Not.Null, ScopePrefabPath);
        Assert.That(PrefabUtility.IsPartOfPrefabAsset(scopePrefab), Is.True);
        Assert.That(scopePrefab.GetComponentsInChildren<ProjectLifetimeScope>(true), Has.Length.EqualTo(1));
        Assert.That(scopePrefab.GetComponentsInChildren<SceneLoader>(true), Has.Length.EqualTo(1));
    }

    [Test]
    public void UIScene_ContainsOnlyItsSceneLifetimeScope()
    {
        SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(UIScenePath);
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();

        Assert.That(sceneAsset, Is.Not.Null, UIScenePath);

        try
        {
            Scene scene = EditorSceneManager.OpenScene(UIScenePath, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            ProjectLifetimeScope[] projectScopes = roots
                .SelectMany(root => root.GetComponentsInChildren<ProjectLifetimeScope>(true))
                .ToArray();
            SceneLoader[] sceneLoaders = roots
                .SelectMany(root => root.GetComponentsInChildren<SceneLoader>(true))
                .ToArray();
            UISceneLifetimeScope[] uiScopes = roots
                .SelectMany(root => root.GetComponentsInChildren<UISceneLifetimeScope>(true))
                .ToArray();

            Assert.That(projectScopes, Has.Length.EqualTo(0));
            Assert.That(sceneLoaders, Has.Length.EqualTo(0));
            Assert.That(uiScopes, Has.Length.EqualTo(1));
        }
        finally
        {
            if (originalSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            }
        }
    }
}
