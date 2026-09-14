using System.Reflection;
using EnemySystem;
using NUnit.Framework;
using R3;
using Services;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>数量通知中的停止、重开不能让旧调度继续向新局生成。</summary>
    public sealed class PopulationEventTests
    {
        [Test]
        public void RestartFromCountEventCancelsOldSchedule()
        {
            GameObject owner = new("PopulationEventTest");
            GameObject prefab = new("PopulationPrefabTest");
            FakeSpawner spawner = new();
            try
            {
                EnemyPopulationController controller = owner.AddComponent<EnemyPopulationController>();
                Set(controller, "normalPrefab", prefab.AddComponent<Enemy>());
                Set(controller, "_spawner", spawner);
                bool restarted = false;
                using var subscription = spawner.OnActiveEnemyCountChanged.Subscribe(count =>
                {
                    if (count == 1 && !restarted)
                    {
                        restarted = true;
                        controller.StopPopulation();
                        controller.StartPopulation(null);
                    }
                });
                controller.StartPopulation(null);
                Advance(controller, 5f);
                Assert.That(spawner.TotalSpawnCalls, Is.EqualTo(1));
                Assert.That(spawner.ActiveEnemyCount, Is.Zero);
                Advance(controller, 5f);
                Assert.That(spawner.ActiveEnemyCount, Is.EqualTo(500));
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(prefab);
                spawner.Dispose();
            }
        }

        [Test]
        public void ZeroTimeAndPauseDoNotStartRefill()
        {
            GameObject owner = new("PopulationPauseTest");
            GameObject prefab = new("PopulationPrefabTest");
            FakeSpawner spawner = new();
            try
            {
                EnemyPopulationController controller = owner.AddComponent<EnemyPopulationController>();
                Set(controller, "normalPrefab", prefab.AddComponent<Enemy>());
                Set(controller, "_spawner", spawner);
                int starts = 0;
                using var subscription = controller.OnRefillStarted.Subscribe(_ => starts++);
                controller.StartPopulation(null);
                Advance(controller, 5f);
                spawner.ClearAll();
                Advance(controller, 0f);
                controller.PausePopulation();
                Advance(controller, 5f);
                Assert.That(starts, Is.Zero);
                Assert.That(spawner.ActiveEnemyCount, Is.Zero);
                controller.ResumePopulation();
                Advance(controller, 1f);
                Assert.That(starts, Is.EqualTo(1));
                Assert.That(spawner.ActiveEnemyCount, Is.EqualTo(50));
            }
            finally
            {
                Object.DestroyImmediate(owner);
                Object.DestroyImmediate(prefab);
                spawner.Dispose();
            }
        }

        private static void Set(object owner, string name, object value)
            => owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, value);

        private static void Advance(EnemyPopulationController controller, float seconds)
            => typeof(EnemyPopulationController).GetMethod("AdvancePopulation", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(controller, new object[] { seconds });

        private sealed class FakeSpawner : IEnemySpawner, System.IDisposable
        {
            private readonly Subject<int> _changed = new();
            public int ActiveEnemyCount { get; private set; }
            public int TotalSpawnCalls { get; private set; }
            public Observable<int> OnActiveEnemyCountChanged => _changed;
            public int CountRank(EnemyRank rank) => rank == EnemyRank.Normal ? ActiveEnemyCount : 0;

            public bool TrySpawnEnemy(Enemy prefab, EnemyRank rank, float healthMultiplier, float speedMultiplier)
            {
                TotalSpawnCalls++;
                _changed.OnNext(++ActiveEnemyCount);
                return true;
            }

            public void ClearAll()
            {
                ActiveEnemyCount = 0;
                _changed.OnNext(0);
            }
            public void KillAllEnemies() => ClearAll();
            public void Dispose() => _changed.Dispose();
        }
    }
}
