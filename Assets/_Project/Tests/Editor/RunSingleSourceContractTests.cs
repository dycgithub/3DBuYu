using System;
using System.IO;
using System.Reflection;
using GameSystem;
using NUnit.Framework;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 锁定单局时间、结算和终局状态的边界，避免旧时间派生规则重新进入主链路。
    /// </summary>
    public sealed class RunSingleSourceContractTests
    {
        private const string GameManagerPath = "Assets/_Project/Scripts/Run/Implement/GameManager.cs";
        private const string GameSessionPath = "Assets/_Project/Scripts/Run/Model/GameSession.cs";
        private const string RunRuleMathPath = "Assets/_Project/Scripts/Run/Model/RunRuleMath.cs";
        private const string StatisticsViewPath = "Assets/_Project/UIToolKit/GameintStatisticsUIToolkit.cs";

        [Test]
        public void GameSession_AdvancesOnlyElapsedTime()
        {
            MethodInfo advanceTime = typeof(GameSession).GetMethod(
                "AdvanceTime",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { typeof(float) },
                null);

            Assert.That(advanceTime, Is.Not.Null);
            if (advanceTime == null)
            {
                return;
            }

            GameSession session = new();
            advanceTime.Invoke(session, new object[] { 1.5f });
            advanceTime.Invoke(session, new object[] { 2f });

            Assert.That(session.ElapsedTime, Is.EqualTo(3.5f));
            PropertyInfo[] properties = typeof(GameSession).GetProperties(
                BindingFlags.Instance | BindingFlags.Public);
            for (int index = 0; index < properties.Length; index++)
            {
                Assert.That(properties[index].Name.StartsWith("Over", StringComparison.Ordinal), Is.False);
            }
        }

        [Test]
        public void SettlementReward_UsesOnlySessionPointsAndDifficulty()
        {
            MethodInfo settlement = typeof(RunRuleMath).GetMethod(
                "CalculateSettlementReward",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[] { typeof(int), typeof(float) },
                null);

            Assert.That(settlement, Is.Not.Null);
            if (settlement == null)
            {
                return;
            }

            Assert.That(settlement.Invoke(null, new object[] { 100, 1.5f }), Is.EqualTo(150));
        }

        [Test]
        public void RunFlow_DoesNotUseTimeDerivedEnergyMultiplier()
        {
            string gameManagerSource = ReadProjectFile(GameManagerPath);
            string runRuleMathSource = ReadProjectFile(RunRuleMathPath);
            string statisticsViewSource = ReadProjectFile(StatisticsViewPath);
            string costMultiplier = "Cost" + "Multiplier";
            string overtime = "Over" + "time";

            Assert.That(gameManagerSource, Does.Not.Contain(costMultiplier));
            Assert.That(runRuleMathSource, Does.Not.Contain(overtime));
            Assert.That(statisticsViewSource, Does.Not.Contain(overtime));
        }

        [Test]
        public void GameManager_DoesNotEnterTerminalStateFromMenu()
        {
            GameObject managerObject = new("MenuTerminalBoundaryTest");
            managerObject.SetActive(false);
            GameManager manager = managerObject.AddComponent<GameManager>();

            try
            {
                manager.Settle();
                manager.GameOver();

                Assert.That(manager.CurrentState, Is.EqualTo(GameState.Menu));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(managerObject);
            }
        }

        private static string ReadProjectFile(string projectPath)
        {
            string absolutePath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                projectPath.Replace('/', Path.DirectorySeparatorChar));
            return File.ReadAllText(absolutePath);
        }
    }
}
