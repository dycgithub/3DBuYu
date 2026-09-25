using System;
using System.Collections.Generic;
using System.Reflection;
using _Project.UI.Common;
using NUnit.Framework;
using Services;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 锁定 UI 自动注入覆盖范围，以及两个场景子容器的背包服务生命周期。
    /// </summary>
    public sealed class UIInventoryInjectionLifecycleTests
    {
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";
        private const string UIScenePath = "Assets/_Project/Scenes/UIScene.unity";

        private static readonly FieldInfo AutoInjectField = typeof(LifetimeScope).GetField(
            "autoInjectGameObjects",
            BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly List<Scene> _openedScenes = new();

        [TearDown]
        public void TearDown()
        {
            for (int index = _openedScenes.Count - 1; index >= 0; index--)
            {
                Scene scene = _openedScenes[index];
                if (scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }

            _openedScenes.Clear();
        }

        [Test]
        public void GameScene_InjectsCardViews()
        {
            Scene scene = OpenScene(GameScenePath);
            GameLoopLifetimeScope scope = FindComponent<GameLoopLifetimeScope>(scene);
            GameObject cardHand = FindObject(scene, "CardHandRoot");
            GameObject actionPanel = FindObject(scene, "CardActionPanel");

            Assert.That(scope, Is.Not.Null);
            Assert.That(cardHand, Is.Not.Null);
            Assert.That(actionPanel, Is.Not.Null);
            Assert.That(actionPanel.activeSelf, Is.False);

            IReadOnlyList<GameObject> targets = GetAutoInjectTargets(scope);
            Assert.That(targets, Does.Contain(cardHand));
            Assert.That(targets, Does.Contain(actionPanel));
        }

        [Test]
        public void UIScene_InjectsFixedTooltip()
        {
            Scene scene = OpenScene(UIScenePath);
            UISceneLifetimeScope scope = FindComponent<UISceneLifetimeScope>(scene);
            GameObject fixedTooltip = FindObject(scene, "ItemInfoPanel");

            Assert.That(scope, Is.Not.Null);
            Assert.That(fixedTooltip, Is.Not.Null);

            IReadOnlyList<GameObject> targets = GetAutoInjectTargets(scope);
            Assert.That(targets, Has.Count.EqualTo(1));
            Assert.That(IsAncestor(targets[0].transform, fixedTooltip.transform), Is.True);
        }

        [Test]
        public void Installers_IsolateInventory()
        {
            IObjectResolver fixedResolver = BuildInstaller(new UISceneInventoryInstaller(
                new ItemTooltipConfiguration
                {
                    HoverEnabled = false,
                    SelectedEnabled = true,
                }));
            IObjectResolver movingResolver = BuildInstaller(new GameLoopInventoryInstaller(
                new ItemTooltipConfiguration
                {
                    HoverEnabled = true,
                    SelectedEnabled = false,
                }));

            try
            {
                IInventorySelectionState fixedSelection =
                    fixedResolver.Resolve<IInventorySelectionState>();
                IInventorySelectionState movingSelection =
                    movingResolver.Resolve<IInventorySelectionState>();
                IItemTooltipService fixedTooltip = fixedResolver.Resolve<IItemTooltipService>();
                IItemTooltipService movingTooltip = movingResolver.Resolve<IItemTooltipService>();

                Assert.That(fixedSelection, Is.Not.SameAs(movingSelection));
                Assert.That(fixedTooltip, Is.Not.SameAs(movingTooltip));
                Assert.That(GetMode(fixedTooltip), Is.EqualTo(ItemTooltipMode.Fixed));
                Assert.That(GetMode(movingTooltip), Is.EqualTo(ItemTooltipMode.Moving));
            }
            finally
            {
                fixedResolver.Dispose();
                movingResolver.Dispose();
            }
        }

        private static IObjectResolver BuildInstaller(IInstaller installer)
        {
            ContainerBuilder builder = new();
            builder.RegisterInstance<IShopService>(new EmptyShopService());
            installer.Install(builder);
            return builder.Build();
        }

        private static ItemTooltipMode GetMode(IItemTooltipService service)
        {
            FieldInfo modeField = typeof(ItemTooltipManager).GetField(
                "_mode",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(modeField, Is.Not.Null);
            return (ItemTooltipMode)modeField.GetValue(service);
        }

        private Scene OpenScene(string path)
        {
            Scene loadedScene = SceneManager.GetSceneByPath(path);
            if (loadedScene.IsValid() && loadedScene.isLoaded)
            {
                return loadedScene;
            }

            loadedScene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            _openedScenes.Add(loadedScene);
            return loadedScene;
        }

        private static IReadOnlyList<GameObject> GetAutoInjectTargets(LifetimeScope scope)
        {
            Assert.That(AutoInjectField, Is.Not.Null);
            return (IReadOnlyList<GameObject>)AutoInjectField.GetValue(scope);
        }

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

        private static GameObject FindObject(Scene scene, string objectName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                Transform[] transforms = roots[rootIndex].GetComponentsInChildren<Transform>(true);
                for (int childIndex = 0; childIndex < transforms.Length; childIndex++)
                {
                    if (transforms[childIndex].name == objectName)
                    {
                        return transforms[childIndex].gameObject;
                    }
                }
            }

            return null;
        }

        private static bool IsAncestor(Transform ancestor, Transform child)
        {
            for (Transform current = child; current != null; current = current.parent)
            {
                if (current == ancestor)
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class EmptyShopService : IShopService
        {
            public int GetPrice(ItemVM item) => 0;

            public bool TryPurchase(ItemVM item) => false;

            public void Refund(ItemVM item)
            {
            }

            public void RefreshShop(GridView shopGrid)
            {
            }
        }
    }
}
