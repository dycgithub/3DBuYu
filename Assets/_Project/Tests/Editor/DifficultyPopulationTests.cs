using System;
using System.Collections.Generic;
using System.Reflection;
using EnemySystem;
using GameSystem;
using NUnit.Framework;
using R3;
using Services;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证运行中切换难度时只调整后续生成，不清除当前敌人，也不会让降难度调度卡在首次阶段。
    /// </summary>
    public sealed class DifficultyPopulationTests
    {
        private const string DifficultyRoot = "Assets/_Project/SO/GamePlaySettings/Difficulties/";

        [Test]
        public void RetargetingToHigherQuota_KeepsExistingEnemiesAndFillsNewQuota()
        {
            int[] counts = { 500, 0, 0 };
            EnemyPopulationSchedule schedule = new(
                EnemyQuota.ForDifficulty(DifficultyLevel.Normal),
                counts);

            schedule.Tick(5f, Count, Spawn);

            Assert.That(counts, Is.EqualTo(new[] { 800, 1, 0 }));
            Assert.That(schedule.InitialCompleted, Is.True);

            int Count(EnemyRank rank) => counts[(int)rank];

            bool Spawn(EnemyRank rank)
            {
                counts[(int)rank]++;
                return true;
            }
        }

        [Test]
        public void RetargetingToLowerQuota_DoesNotClearOrStallAboveNewLimit()
        {
            int[] counts = { 600, 0, 0 };
            EnemyPopulationSchedule schedule = new(
                EnemyQuota.ForDifficulty(DifficultyLevel.Easy),
                counts);
            int spawnCalls = 0;

            schedule.Tick(1f, Count, Spawn);

            Assert.That(counts, Is.EqualTo(new[] { 600, 0, 0 }));
            Assert.That(spawnCalls, Is.Zero);
            Assert.That(schedule.InitialCompleted, Is.True);

            int Count(EnemyRank rank) => counts[(int)rank];

            bool Spawn(EnemyRank rank)
            {
                spawnCalls++;
                counts[(int)rank]++;
                return true;
            }
        }

        [Test]
        public void ControllerChangeDifficulty_UpdatesQuotaWithoutClearingSpawner()
        {
            GameObject owner = new("DifficultyPopulationControllerTest");
            GameObject normalObject = new("NormalPrefab");
            GameObject eliteObject = new("ElitePrefab");
            GameObject bossObject = new("BossPrefab");
            FakeSpawner spawner = new();

            try
            {
                EnemyPopulationController controller = owner.AddComponent<EnemyPopulationController>();
                Set(controller, "normalPrefab", normalObject.AddComponent<Enemy>());
                Set(controller, "elitePrefab", eliteObject.AddComponent<Enemy>());
                Set(controller, "bossPrefab", bossObject.AddComponent<Enemy>());
                Set(controller, "_spawner", spawner);

                DifficultyConfig easy = Load("Easy");
                DifficultyConfig normal = Load("Normal");
                controller.StartPopulation(easy);
                Advance(controller, 5f);

                Assert.That(spawner.ActiveEnemyCount, Is.EqualTo(500));
                Assert.That(controller.TryChangeDifficulty(normal), Is.True);
                Assert.That(controller.Difficulty, Is.EqualTo(DifficultyLevel.Normal));
                Assert.That(controller.MaximumPopulation, Is.EqualTo(801));
                Assert.That(spawner.ActiveEnemyCount, Is.EqualTo(500));

                Advance(controller, 5f);

                Assert.That(spawner.ActiveEnemyCount, Is.EqualTo(801));
                Assert.That(spawner.CountRank(EnemyRank.Normal), Is.EqualTo(800));
                Assert.That(spawner.CountRank(EnemyRank.Elite), Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(normalObject);
                Object.DestroyImmediate(eliteObject);
                Object.DestroyImmediate(bossObject);
                spawner.Dispose();
            }
        }

        private static DifficultyConfig Load(string name)
        {
            DifficultyConfig config = AssetDatabase.LoadAssetAtPath<DifficultyConfig>(
                $"{DifficultyRoot}{name}.asset");
            Assert.That(config, Is.Not.Null, $"缺少难度资产: {name}");
            return config;
        }

        private static void Set(object owner, string name, object value)
        {
            FieldInfo field = owner.GetType().GetField(
                name,
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, $"找不到字段: {name}");
            field.SetValue(owner, value);
        }

        private static void Advance(EnemyPopulationController controller, float seconds)
        {
            MethodInfo method = typeof(EnemyPopulationController).GetMethod(
                "AdvancePopulation",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null);
            method.Invoke(controller, new object[] { seconds });
        }

        private sealed class FakeSpawner : IEnemySpawner, IDisposable
        {
            private readonly Subject<int> _changed = new();
            private readonly int[] _counts = new int[3];

            public int ActiveEnemyCount => _counts[0] + _counts[1] + _counts[2];
            public Observable<int> OnActiveEnemyCountChanged => _changed;

            public bool TrySpawnEnemy(
                Enemy prefab,
                EnemyRank rank,
                float healthMultiplier,
                float speedMultiplier)
            {
                _counts[(int)rank]++;
                _changed.OnNext(ActiveEnemyCount);
                return true;
            }

            public int CountRank(EnemyRank rank)
            {
                return _counts[(int)rank];
            }

            public void ClearAll()
            {
                Array.Clear(_counts, 0, _counts.Length);
                _changed.OnNext(0);
            }

            public void KillAllEnemies()
            {
                ClearAll();
            }

            public void Dispose()
            {
                _changed.Dispose();
            }
        }
    }
}
