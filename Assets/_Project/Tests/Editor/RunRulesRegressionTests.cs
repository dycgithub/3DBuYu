using System;
using System.Reflection;
using GameSystem;
using NUnit.Framework;
using Services;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证击杀积分、能量耗尽结算和 GameScene 融球 HUD 的公开契约。
    /// </summary>
    public sealed class RunRulesRegressionTests
    {
        private const string GameScenePath = "Assets/_Project/Scenes/GameScene.unity";

        [Test]
        public void GameManager_OnEnemyKilled_PublishesBasePointsAndKeepsRawSessionPoints()
        {
            GameObject managerObject = new("RawKillPointsTest");
            managerObject.SetActive(false);
            GameManager manager = managerObject.AddComponent<GameManager>();
            IGameEventService gameEvents = manager;
            int publishedPoints = -1;
            Action<int> pointsHandler = points => publishedPoints = points;
            gameEvents.EnemyKilled += pointsHandler;

            try
            {
                manager.StateMachine.ChangeState(GameState.Playing);
                manager.Session.AdvanceTime(310f);

                int returnedPoints = manager.OnEnemyKilled(100);

                Assert.That(returnedPoints, Is.EqualTo(100));
                Assert.That(publishedPoints, Is.EqualTo(100));
                Assert.That(manager.SessionPoints, Is.EqualTo(100));
                Assert.That(manager.Session.KillCount, Is.EqualTo(1));
            }
            finally
            {
                gameEvents.EnemyKilled -= pointsHandler;
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void GameScene_MetaballEnergyRingUsesMenuRootAndKeepsPointPanel()
        {
            string originalScenePath = SceneManager.GetActiveScene().path;
            EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Single);

            try
            {
                Transform menuRoot = GameObject.Find("Canvas/MetaballMenuRoot")?.transform;
                Assert.That(menuRoot, Is.Not.Null);

                Transform energyRing = menuRoot?.Find("EnergyRing");
                Assert.That(energyRing, Is.Not.Null);

                RectTransform energyRect = energyRing?.GetComponent<RectTransform>();
                Assert.That(energyRect, Is.Not.Null);
                Assert.That(energyRect.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(energyRect.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
                Assert.That(energyRect.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(energyRect.sizeDelta, Is.EqualTo(new Vector2(82f, 82f)));

                Assert.That(menuRoot?.Find("CoreLabel"), Is.Null);
                Assert.That(menuRoot?.Find("Instruction"), Is.Null);
                Assert.That(menuRoot?.Find("SettingsItem/ProgressRing"), Is.Null);
                Assert.That(menuRoot?.Find("SupplyItem/ProgressRing"), Is.Null);
                Assert.That(menuRoot?.Find("BackpackItem/ProgressRing"), Is.Null);

                string uiSceneSource = System.IO.File.ReadAllText(
                    System.IO.Path.Combine(Application.dataPath, "_Project/Scenes/UIScene.unity"));
                Assert.That(uiSceneSource, Does.Contain("m_Name: PointPanel"));
            }
            finally
            {
                if (!string.IsNullOrEmpty(originalScenePath) && originalScenePath != GameScenePath)
                {
                    EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
                }
            }
        }

        [Test]
        public void GameManager_EnergyDepletedBeforeRequirements_FailsWithoutReward()
        {
            GameObject managerObject = new("EarlyEnergyDepletionTest");
            managerObject.SetActive(false);
            GameManager manager = managerObject.AddComponent<GameManager>();
            EnergyService energy = new();
            StageConfig stage = ScriptableObject.CreateInstance<StageConfig>();
            bool wasSuccess = true;

            try
            {
                stage.timeLimit = 300f;
                stage.targetKillCount = 1;
                SetField(manager, "stageConfig", stage);
                SetField(manager, "_energy", energy);
                SetField(manager, "_runRuleService", new RunRuleService());
                manager.OnSettled += (success, _) => wasSuccess = success;
                manager.StateMachine.ChangeState(GameState.Playing);
                InvokePrivate(manager, "ActivateRunSubscriptions");
                energy.Initialize(1f, 1f);

                energy.Drain(1f, EnergySpendKind.TimeFlow);

                Assert.That(manager.CurrentState, Is.EqualTo(GameState.Failed));
                Assert.That(manager.VictoryReward, Is.Zero);
                Assert.That(wasSuccess, Is.False);
            }
            finally
            {
                energy.Dispose();
                UnityEngine.Object.DestroyImmediate(stage);
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void GameManager_EnergyDepletedAfterRequirements_SettlesRawPoints()
        {
            GameObject managerObject = new("SuccessfulEnergyDepletionTest");
            managerObject.SetActive(false);
            GameManager manager = managerObject.AddComponent<GameManager>();
            EnergyService energy = new();
            StageConfig stage = ScriptableObject.CreateInstance<StageConfig>();
            int settledPoints = 0;
            bool wasSuccess = false;

            try
            {
                stage.timeLimit = 300f;
                stage.targetKillCount = 1;
                SetField(manager, "stageConfig", stage);
                SetField(manager, "_energy", energy);
                SetField(manager, "_runRuleService", new RunRuleService());
                manager.OnSettled += (success, points) =>
                {
                    wasSuccess = success;
                    settledPoints = points;
                };
                manager.Session.RecordKill(100);
                manager.Session.AdvanceTime(300f);
                manager.StateMachine.ChangeState(GameState.Playing);
                InvokePrivate(manager, "ActivateRunSubscriptions");
                energy.Initialize(1f, 1f);

                energy.Drain(1f, EnergySpendKind.TimeFlow);

                Assert.That(manager.CurrentState, Is.EqualTo(GameState.Settled));
                Assert.That(manager.VictoryReward, Is.EqualTo(100));
                Assert.That(wasSuccess, Is.True);
                Assert.That(settledPoints, Is.EqualTo(100));
            }
            finally
            {
                energy.Dispose();
                UnityEngine.Object.DestroyImmediate(stage);
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field?.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, methodName);
            method?.Invoke(target, null);
        }
    }
}
