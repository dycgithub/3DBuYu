using System.IO;
using System.Reflection;
using GameSystem;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证 GameScene 统计面板的运行时类型、难度文案和 UXML 结构。
    /// </summary>
    public sealed class GameintStatisticsUIToolkitTests
    {
        private const string UxmlPath = "Assets/_Project/UIToolKit/GameintStatisticsUIToolkit.uxml";
        private const string UssPath = "Assets/_Project/UIToolKit/GameintStatisticsUIToolkit.uss";

        [Test]
        public void View_IsRuntimeComponent()
        {
            Assert.That(typeof(GameintStatisticsUIToolkit).IsSubclassOf(typeof(MonoBehaviour)), Is.True);
        }

        [TestCase(DifficultyLevel.Easy, "1 简单")]
        [TestCase(DifficultyLevel.Normal, "2 普通")]
        [TestCase(DifficultyLevel.Advanced, "3 进阶")]
        [TestCase(DifficultyLevel.Expert, "4 专家")]
        [TestCase(DifficultyLevel.Nightmare, "5 噩梦")]
        [TestCase(DifficultyLevel.Extreme, "6 究极")]
        [TestCase(DifficultyLevel.Impossible, "7 ??通过")]
        public void FormatDifficulty_UsesConfiguredSevenLevelText(
            DifficultyLevel level,
            string expected)
        {
            MethodInfo method = typeof(GameintStatisticsUIToolkit).GetMethod(
                "FormatDifficulty",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(method, Is.Not.Null);
            string actual = (string)method.Invoke(null, new object[] { level });

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void Uxml_ContainsAllStatisticsLabels()
        {
            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            Assert.That(asset, Is.Not.Null);

            VisualElement root = asset.Instantiate();

            Assert.That(root.Q<Label>("difficulty-label"), Is.Not.Null);
            Assert.That(root.Q<Label>("current-streak-label"), Is.Not.Null);
            Assert.That(root.Q<Label>("best-streak-label"), Is.Not.Null);
            Assert.That(root.Q<Label>("total-kills-label"), Is.Not.Null);
            Assert.That(root.Q<Label>("enemy-count-label"), Is.Not.Null);
            Assert.That(root.Q<Label>("global-points-label"), Is.Not.Null);
            Assert.That(root.Q<Label>("elapsed-time-label"), Is.Not.Null);
            Assert.That(root.Q<Label>("victory-reward-label"), Is.Not.Null);
            Assert.That(root.Q<Label>("victory-reward-formula-label"), Is.Null);
            Assert.That(root.Q<Label>("remaining-points-label"), Is.Null);
        }

        [Test]
        public void Uxml_UsesOneCompleteVictoryRewardLabel()
        {
            VisualTreeAsset asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            Assert.That(asset, Is.Not.Null);

            VisualElement root = asset.Instantiate();
            Assert.That(root.Q<VisualElement>("victory-reward-group"), Is.Null);
            Assert.That(root.Q<Label>("victory-reward-label"), Is.Not.Null);
        }

        [Test]
        public void Uss_UsesA300SquareTextOnlyPanel()
        {
            string uss = File.ReadAllText(UssPath);

            Assert.That(uss, Does.Contain("width: 300px;"));
            Assert.That(uss, Does.Contain("height: 300px;"));
            Assert.That(uss, Does.Not.Contain("background-image"));
            Assert.That(uss, Does.Not.Contain("background-color"));
        }

        [TestCase("Easy", 1f)]
        [TestCase("Normal", 2f)]
        [TestCase("Advanced", 3f)]
        [TestCase("Expert", 5f)]
        [TestCase("Nightmare", 7f)]
        [TestCase("Extreme", 10f)]
        [TestCase("Impossible", 20f)]
        public void DifficultyAssets_UseConfiguredSettlementMultiplier(
            string assetName,
            float expectedMultiplier)
        {
            string path = $"Assets/_Project/SO/GamePlaySettings/Difficulties/{assetName}.asset";
            DifficultyConfig difficulty = AssetDatabase.LoadAssetAtPath<DifficultyConfig>(path);

            Assert.That(difficulty, Is.Not.Null);
            Assert.That(difficulty.SettlementMultiplier, Is.EqualTo(expectedMultiplier));
        }
    }
}
