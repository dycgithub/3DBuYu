using System.IO;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 锁定 GameScene 的输入组件和 GameLoop 子容器注册，防止玩家控制器失去 IInputService。
    /// </summary>
    public sealed class PlayerInputInjectionContractTests
    {
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";
        private const string ScopePath = "Assets/_Project/Scripts/Composition/Scopes/GameLoopLifetimeScope.cs";

        private Scene _openedScene;

        [TearDown]
        public void TearDown()
        {
            if (_openedScene.IsValid() && _openedScene.isLoaded)
            {
                EditorSceneManager.CloseScene(_openedScene, true);
            }

            _openedScene = default;
        }

        [Test]
        public void GameScene_ContainsInputServiceUnderGameLoopScope()
        {
            _openedScene = EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);
            GameLoopLifetimeScope scope = FindComponent<GameLoopLifetimeScope>(_openedScene);

            Assert.That(scope, Is.Not.Null);
            Assert.That(scope.GetComponentInChildren<InputSystem.InputService>(true), Is.Not.Null);
        }

        [Test]
        public void GameLoopScope_RegistersInputService()
        {
            string source = File.ReadAllText(ScopePath);

            StringAssert.Contains(
                "RegisterComponentInHierarchy<InputSystem.InputService>()",
                source);
            StringAssert.Contains("As<IInputService>()", source);
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
    }
}
