using System;
using GameSystem;
using NUnit.Framework;
using UnityEditor;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证难度选项的相邻切换、配置缺失处理和前 60 秒时间门禁。
    /// </summary>
    public sealed class DifficultySelectionTests
    {
        private const string DifficultyRoot = "Assets/_Project/SO/GamePlaySettings/Difficulties/";

        private DifficultyConfig _easy;
        private DifficultyConfig _normal;
        private DifficultyConfig _advanced;
        private DifficultyConfig _expert;
        private DifficultyConfig _nightmare;
        private DifficultyConfig _extreme;
        private DifficultyConfig _impossible;

        [SetUp]
        public void SetUp()
        {
            _easy = Load("Easy");
            _normal = Load("Normal");
            _advanced = Load("Advanced");
            _expert = Load("Expert");
            _nightmare = Load("Nightmare");
            _extreme = Load("Extreme");
            _impossible = Load("Impossible");
        }

        [Test]
        public void IncreaseAndDecrease_FollowsDifficultyLevelRegardlessOfAssetOrder()
        {
            DifficultySelection selection = new(
                _easy,
                new[]
                {
                    _impossible,
                    _expert,
                    _normal,
                    _extreme,
                    _easy,
                    _nightmare,
                    _advanced
                });

            Assert.That(selection.TryGetTarget(
                DifficultyChangeDirection.Increase,
                0f,
                out DifficultyConfig target), Is.True);
            Assert.That(target, Is.SameAs(_normal));

            selection.Commit(target);
            Assert.That(selection.TryGetTarget(
                DifficultyChangeDirection.Decrease,
                10f,
                out target), Is.True);
            Assert.That(target, Is.SameAs(_easy));
        }

        [Test]
        public void IncreaseAndDecrease_StopAtTheConfiguredEnds()
        {
            DifficultySelection selection = new(
                _easy,
                new[] { _easy, _normal, _advanced, _expert, _nightmare, _extreme, _impossible });

            Assert.That(selection.TryGetTarget(
                DifficultyChangeDirection.Decrease,
                0f,
                out _), Is.False);

            selection.Commit(_impossible);
            Assert.That(selection.TryGetTarget(
                DifficultyChangeDirection.Increase,
                0f,
                out _), Is.False);
        }

        [TestCase(0f, true)]
        [TestCase(59.999f, true)]
        [TestCase(60f, false)]
        [TestCase(60.001f, false)]
        [TestCase(-0.001f, false)]
        [TestCase(float.NaN, false)]
        [TestCase(float.PositiveInfinity, false)]
        public void TryGetTarget_UsesStrictFiniteNonNegativeSixtySecondWindow(
            float elapsedTime,
            bool expected)
        {
            DifficultySelection selection = new(
                _easy,
                new[] { _easy, _normal });

            bool actual = selection.TryGetTarget(
                DifficultyChangeDirection.Increase,
                elapsedTime,
                out _);

            Assert.That(actual, Is.EqualTo(expected));
        }

        [Test]
        public void MissingAdjacentConfiguration_RejectsChangeWithoutMutatingCurrent()
        {
            DifficultySelection selection = new(_easy, new[] { _easy });

            bool changed = selection.TryGetTarget(
                DifficultyChangeDirection.Increase,
                1f,
                out _);

            Assert.That(changed, Is.False);
            Assert.That(selection.Current, Is.SameAs(_easy));
        }

        private static DifficultyConfig Load(string name)
        {
            DifficultyConfig config = AssetDatabase.LoadAssetAtPath<DifficultyConfig>(
                $"{DifficultyRoot}{name}.asset");
            Assert.That(config, Is.Not.Null, $"缺少难度资产: {name}");
            return config;
        }
    }
}
