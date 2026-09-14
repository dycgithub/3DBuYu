using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CombatSystem;
using EnemySystem;
using GameSystem;
using Interfaces;
using NUnit.Framework;
using R3;
using Services;
using UnityEngine;
using FlockingSystem.ECS;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 锁定 GameManager、数量调度和 Enemy 拆分时不可改变的生命周期边界。
    /// </summary>
    public sealed class GameEnemyLifecycleRegressionTests
    {
        [Test]
        public void GameManager_TickOrderStable()
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "_Project/Scripts/Run/Implement/GameManager.cs");
            string source = File.ReadAllText(sourcePath);

            AssertOrder(
                source,
                "if (_stateMachine.CurrentState != GameState.Playing)",
                "float deltaTime = Time.deltaTime;",
                "_killStreak?.Tick(deltaTime);",
                "_session.AdvanceTime(deltaTime);",
                "OnRunTimeChanged?.Invoke(_session.ElapsedTime);",
                "_energy.Tick(deltaTime, stageConfig != null ? stageConfig.baseEnergyDrainPerSecond : 0f);");
        }

        [Test]
        public void GameManager_PauseLifecycle()
        {
            GameObject managerObject = new("GameManagerPauseRegression");
            managerObject.SetActive(false);
            GameManager manager = managerObject.AddComponent<GameManager>();
            RecordingPauseService pauseService = new();
            RecordingPopulationService populationService = new();

            try
            {
                SetField(manager, "_pauseService", pauseService);
                SetField(manager, "_populationService", populationService);
                manager.StateMachine.ChangeState(GameState.Playing);

                manager.TogglePause();
                Assert.That(manager.CurrentState, Is.EqualTo(GameState.Paused));
                Assert.That(pauseService.PauseCalls, Is.EqualTo(1));
                Assert.That(populationService.PauseCalls, Is.EqualTo(1));

                manager.TogglePause();
                Assert.That(manager.CurrentState, Is.EqualTo(GameState.Playing));
                Assert.That(pauseService.ResumeCalls, Is.EqualTo(1));
                Assert.That(populationService.ResumeCalls, Is.EqualTo(1));
            }
            finally
            {
                populationService.Dispose();
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void GameManager_SettleOnce()
        {
            GameObject managerObject = new("GameManagerSettlementRegression");
            managerObject.SetActive(false);
            GameManager manager = managerObject.AddComponent<GameManager>();
            int settlementCount = 0;

            try
            {
                manager.OnSettled += (_, _) => settlementCount++;
                manager.StateMachine.ChangeState(GameState.Playing);

                manager.Settle();
                manager.Settle();
                manager.GameOver();

                Assert.That(settlementCount, Is.EqualTo(1));
                Assert.That(manager.CurrentState, Is.EqualTo(GameState.Settled));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void Population_PauseDoesNotAdvance()
        {
            GameObject owner = new("PopulationPauseRegression");
            GameObject prefab = new("PopulationPausePrefab");
            FakeSpawner spawner = new();

            try
            {
                EnemyPopulationController controller = owner.AddComponent<EnemyPopulationController>();
                SetField(controller, "normalPrefab", prefab.AddComponent<Enemy>());
                SetField(controller, "_spawner", spawner);
                controller.StartPopulation(null);
                AdvancePopulation(controller, 5f);
                spawner.ClearAll();

                controller.PausePopulation();
                AdvancePopulation(controller, 1f);
                Assert.That(spawner.ActiveEnemyCount, Is.Zero);

                controller.ResumePopulation();
                AdvancePopulation(controller, 1f);
                Assert.That(spawner.ActiveEnemyCount, Is.EqualTo(50));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(prefab);
                spawner.Dispose();
            }
        }

        [Test]
        public void Population_StopCancelsOldRun()
        {
            GameObject owner = new("PopulationStopRegression");
            GameObject prefab = new("PopulationStopPrefab");
            FakeSpawner spawner = new();

            try
            {
                EnemyPopulationController controller = owner.AddComponent<EnemyPopulationController>();
                SetField(controller, "normalPrefab", prefab.AddComponent<Enemy>());
                SetField(controller, "_spawner", spawner);
                controller.StartPopulation(null);
                controller.StartPopulation(null);
                AdvancePopulation(controller, 5f);
                Assert.That(spawner.ActiveEnemyCount, Is.EqualTo(500));

                controller.StopPopulation();
                AdvancePopulation(controller, 5f);
                Assert.That(spawner.ActiveEnemyCount, Is.Zero);

                controller.StartPopulation(null);
                AdvancePopulation(controller, 1f);
                Assert.That(spawner.ActiveEnemyCount, Is.EqualTo(100));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(owner);
                UnityEngine.Object.DestroyImmediate(prefab);
                spawner.Dispose();
            }
        }

        [Test]
        public void Enemy_HighlightBeforeDied()
        {
            GameObject enemyObject = new("EnemyHighlightRegression");
            EnemyAttributesSO attributes = ScriptableObject.CreateInstance<EnemyAttributesSO>();

            try
            {
                enemyObject.SetActive(false);
                Enemy enemy = enemyObject.AddComponent<Enemy>();
                attributes.baseHealth = 10f;
                attributes.hitEffect = EffectSystem.EffectId.None;
                attributes.deathEffect = EffectSystem.EffectId.None;
                SetField(enemy, "enemyAttributes", attributes);
                enemyObject.SetActive(true);
                enemy.InitializeForSpawn(1f, 1f);

                bool sawHighlight = false;
                enemy.OnDied += _ =>
                {
                    Color activeColor = GetField<Color>(enemy, "_activeFlashColor");
                    float highlightTimer = GetField<float>(enemy, "_damageFlashTimer");
                    sawHighlight = activeColor == new Color(1f, 0.75f, 0.2f, 1f) && highlightTimer > 0f;
                };

                DamageResult result = enemy.ReceiveDamageRequest(new DamageRequest
                {
                    DamageAmount = 10f,
                    DamageType = CombatSystem.DamageType.Physical,
                });

                Assert.That(result.WasKilled, Is.True);
                Assert.That(sawHighlight, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyObject);
                UnityEngine.Object.DestroyImmediate(attributes);
            }
        }

        [Test]
        public void Enemy_SpatialLifecycle()
        {
            GameObject enemyObject = new("EnemySpatialRegression");
            EnemyAttributesSO attributes = ScriptableObject.CreateInstance<EnemyAttributesSO>();
            EnemyFlockSettingsSO flockSettings = ScriptableObject.CreateInstance<EnemyFlockSettingsSO>();
            RecordingSpatialService spatialService = new();

            try
            {
                enemyObject.SetActive(false);
                Enemy enemy = enemyObject.AddComponent<Enemy>();
                SetField(enemy, "enemyAttributes", attributes);
                SetField(enemy, "_spatialService", spatialService);
                SetField(enemy, "_flockSettings", flockSettings);

                enemyObject.SetActive(true);
                Assert.That(spatialService.RegisterCalls, Is.EqualTo(1));
                Assert.That(spatialService.LastRegisteredEntity, Is.SameAs(enemy));

                enemyObject.SetActive(false);
                Assert.That(spatialService.UnregisterCalls, Is.EqualTo(1));

                enemyObject.SetActive(true);
                Assert.That(spatialService.RegisterCalls, Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyObject);
                UnityEngine.Object.DestroyImmediate(attributes);
                UnityEngine.Object.DestroyImmediate(flockSettings);
            }
        }

        [Test]
        public void Enemy_ResetClearsState()
        {
            GameObject enemyObject = new("EnemyResetRegression");
            EnemyAttributesSO attributes = ScriptableObject.CreateInstance<EnemyAttributesSO>();

            try
            {
                enemyObject.SetActive(false);
                Enemy enemy = enemyObject.AddComponent<Enemy>();
                attributes.baseHealth = 10f;
                attributes.hitEffect = EffectSystem.EffectId.None;
                attributes.deathEffect = EffectSystem.EffectId.None;
                SetField(enemy, "enemyAttributes", attributes);
                enemyObject.SetActive(true);
                enemy.InitializeForSpawn(1f, 1f);
                enemy.SpeedMultiplier = 3f;
                enemy.Kill();

                enemy.ResetForReuse();

                Assert.That(enemy.IsDead, Is.False);
                Assert.That(enemy.GetComponent<Collider>().enabled, Is.True);
                Assert.That(enemy.CurrentHealth, Is.Zero);
                Assert.That(enemy.MaxHealth, Is.Zero);
                Assert.That(enemy.SpeedMultiplier, Is.EqualTo(1f));
                Assert.That(GetField<float>(enemy, "_damageFlashTimer"), Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(enemyObject);
                UnityEngine.Object.DestroyImmediate(attributes);
            }
        }

        [Test]
        public void Enemy_UsesSpawnInitializer()
        {
            MethodInfo method = typeof(Enemy).GetMethod(
                "InitializeForSpawn",
                BindingFlags.Instance | BindingFlags.Public);

            Assert.That(method, Is.Not.Null);
            if (method == null)
            {
                return;
            }

            ParameterInfo[] parameters = method.GetParameters();
            Assert.That(method.ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(parameters.Length, Is.EqualTo(2));
            Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(float)));
            Assert.That(parameters[1].ParameterType, Is.EqualTo(typeof(float)));
        }

        private static void AdvancePopulation(EnemyPopulationController controller, float seconds)
        {
            MethodInfo method = typeof(EnemyPopulationController).GetMethod(
                "AdvancePopulation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            if (method != null)
            {
                method.Invoke(controller, new object[] { seconds });
            }
        }

        private static void AssertOrder(string source, params string[] fragments)
        {
            int previousIndex = -1;
            for (int index = 0; index < fragments.Length; index++)
            {
                int currentIndex = source.IndexOf(fragments[index], StringComparison.Ordinal);
                Assert.That(currentIndex, Is.GreaterThan(previousIndex), fragments[index]);
                previousIndex = currentIndex;
            }
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            if (field != null)
            {
                field.SetValue(target, value);
            }
        }

        private static T GetField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return field != null ? (T)field.GetValue(target) : default;
        }

        private sealed class RecordingPauseService : IGamePauseService
        {
            public bool IsPaused { get; private set; }
            public int PauseCalls { get; private set; }
            public int ResumeCalls { get; private set; }
            public event Action<bool> PauseStateChanged;

            public void Pause()
            {
                PauseCalls++;
                IsPaused = true;
                PauseStateChanged?.Invoke(true);
            }

            public void Resume()
            {
                ResumeCalls++;
                IsPaused = false;
                PauseStateChanged?.Invoke(false);
            }
        }

        private sealed class RecordingPopulationService : IEnemyPopulationService, IDisposable
        {
            private readonly Subject<int> _aliveChanged = new();
            private readonly Subject<Unit> _started = new();
            private readonly Subject<Unit> _initialCompleted = new();
            private readonly Subject<Unit> _refillStarted = new();
            private readonly Subject<Unit> _refillCompleted = new();

            public int EnemiesAlive => 0;
            public int MaximumPopulation => 0;
            public DifficultyLevel Difficulty => DifficultyLevel.Easy;
            public Observable<int> OnEnemiesAliveChanged => _aliveChanged;
            public Observable<Unit> OnPopulationStarted => _started;
            public Observable<Unit> OnInitialCompleted => _initialCompleted;
            public Observable<Unit> OnRefillStarted => _refillStarted;
            public Observable<Unit> OnRefillCompleted => _refillCompleted;
            public int PauseCalls { get; private set; }
            public int ResumeCalls { get; private set; }

            public void StartPopulation(DifficultyConfig difficulty) { }
            public bool TryChangeDifficulty(DifficultyConfig difficulty) => false;
            public void PausePopulation() => PauseCalls++;
            public void ResumePopulation() => ResumeCalls++;
            public void StopPopulation() { }

            public void Dispose()
            {
                _aliveChanged.Dispose();
                _started.Dispose();
                _initialCompleted.Dispose();
                _refillStarted.Dispose();
                _refillCompleted.Dispose();
            }
        }

        private sealed class FakeSpawner : IEnemySpawner, IDisposable
        {
            private readonly Subject<int> _changed = new();

            public int ActiveEnemyCount { get; private set; }
            public Observable<int> OnActiveEnemyCountChanged => _changed;

            public bool TrySpawnEnemy(
                Enemy prefab,
                EnemyRank rank,
                float healthMultiplier,
                float speedMultiplier)
            {
                ActiveEnemyCount++;
                _changed.OnNext(ActiveEnemyCount);
                return true;
            }

            public int CountRank(EnemyRank rank)
                => rank == EnemyRank.Normal ? ActiveEnemyCount : 0;

            public void ClearAll()
            {
                ActiveEnemyCount = 0;
                _changed.OnNext(0);
            }

            public void KillAllEnemies() => ClearAll();
            public void Dispose() => _changed.Dispose();
        }

        private sealed class RecordingSpatialService : ISpatialQueryService
        {
            public int RegisterCalls { get; private set; }
            public int UnregisterCalls { get; private set; }
            public IDamageable LastRegisteredEntity { get; private set; }

            public int Register(IDamageable entity, float radius, int layerMask)
            {
                RegisterCalls++;
                LastRegisteredEntity = entity;
                return RegisterCalls;
            }

            public void Unregister(int entityId) => UnregisterCalls++;
            public void UpdatePosition(int entityId, Vector3 position) { }
            public IDamageable QueryNearest(Vector3 center, float radius, int layerMask) => null;
            public List<IDamageable> QueryRadius(Vector3 center, float radius, int layerMask) => new();
            public List<IDamageable> QueryAll(int layerMask) => new();
        }
    }
}
