using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using NUnit.Framework;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 锁定 GameScene 中 UGUI 结算面板的挂载位置和控件契约。
    /// </summary>
    public sealed class RunResultPanelTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/GameScene.unity";

        [Test]
        public void PanelUnderCanvas()
        {
            Scene scene = OpenScene();
            try
            {
                GameObject canvas = FindRoot(scene, "Canvas");
                Assert.That(canvas, Is.Not.Null);

                Transform panel = canvas.transform.Find("RunResultPanel");
                Assert.That(panel, Is.Not.Null);
                Assert.That(panel.GetComponent<CanvasGroup>(), Is.Not.Null);
                Assert.That(panel.GetComponent("RunResultPanel"), Is.Not.Null);
            }
            finally
            {
                CloseScene(scene);
            }
        }

        [Test]
        public void PanelHasWidgets()
        {
            Scene scene = OpenScene();
            try
            {
                GameObject canvas = FindRoot(scene, "Canvas");
                Transform panel = canvas?.transform.Find("RunResultPanel");
                Assert.That(panel, Is.Not.Null);

                string[] textNames =
                {
                    "EyebrowLabel",
                    "TitleLabel",
                    "SubtitleLabel",
                    "DifficultyLabel",
                    "ElapsedTimeLabel",
                    "KillCountLabel",
                    "BestStreakLabel",
                    "SessionPointsLabel",
                    "SettlementMultiplierLabel",
                    "SettlementFormulaLabel",
                    "FailureRewardLabel",
                };

                for (int index = 0; index < textNames.Length; index++)
                {
                    Assert.That(
                        panel.Find(textNames[index])?.GetComponent<TextMeshProUGUI>(),
                        Is.Not.Null,
                        textNames[index]);
                }

                Assert.That(panel.Find("VictoryContent"), Is.Not.Null);
                Assert.That(panel.Find("FailureContent"), Is.Not.Null);
                Assert.That(panel.Find("ReturnButton")?.GetComponent<Button>(), Is.Not.Null);
            }
            finally
            {
                CloseScene(scene);
            }
        }

        [Test]
        public void OldPresenterRemoved()
        {
            Scene scene = OpenScene();
            try
            {
                Assert.That(FindObject(scene, "ResultUIToolKit"), Is.Null);
                Assert.That(FindObject(scene, "RunResultCanvas"), Is.Null);
            }
            finally
            {
                CloseScene(scene);
            }
        }

        private static Scene OpenScene()
        {
            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            }

            return scene;
        }

        private static void CloseScene(Scene scene)
        {
            if (scene.IsValid() && scene.isLoaded && scene != SceneManager.GetActiveScene())
            {
                EditorSceneManager.CloseScene(scene, true);
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
    }
}
