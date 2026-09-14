using System.IO;
using GameSystem;
using NUnit.Framework;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 验证单局时间、暂停和能量耗尽胜负规则的公开契约。
    /// </summary>
    public sealed class RunTimingContractTests
    {
        private const string ProjectRoot = "Assets/_Project/";
        private const string GameManagerPath = ProjectRoot + "Scripts/Run/Implement/GameManager.cs";
        private const string PlayerFeedbackPath = ProjectRoot + "Scripts/Play/Combat/PlayerCollisionFeedback.cs";
        private const string DeathExplosionPath = ProjectRoot + "Scripts/Enemy/Components/EnemyDeathExplosionComponent.cs";

        [Test]
        public void GameSession_StoresRunTime()
        {
            GameSession session = new();

            session.AdvanceTime(299f);
            session.AdvanceTime(1f);

            Assert.That(session.ElapsedTime, Is.EqualTo(300f));
            Assert.That(ReadProjectFile(GameManagerPath), Does.Contain("GameSession"));
        }

        [Test]
        public void PausedGame_DoesNotAdvanceElapsedTime()
        {
            GameObject managerObject = new("PausedRunTimeTest");
            managerObject.SetActive(false);
            GameManager manager = managerObject.AddComponent<GameManager>();

            try
            {
                manager.StateMachine.ChangeState(GameState.Paused);
                InvokeUpdate(manager);

                Assert.That(manager.Session.ElapsedTime, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(managerObject);
            }
        }

        [Test]
        public void ContactDeath_KeepRunTime()
        {
            Assert.That(ReadProjectFile(PlayerFeedbackPath), Does.Not.Contain("Timer"));
            Assert.That(ReadProjectFile(DeathExplosionPath), Does.Not.Contain("Timer"));
        }

        [TestCase(299f, 10, 10, RunVerdict.Defeat)]
        [TestCase(300f, 9, 10, RunVerdict.Defeat)]
        [TestCase(300f, 10, 10, RunVerdict.Victory)]
        public void EnergyDepleted_UsesElapsedTimeAndExistingVictoryConditions(
            float elapsedTime,
            int killCount,
            int targetKillCount,
            RunVerdict expectedVerdict)
        {
            RunVerdict verdict = new RunRuleService().JudgeEnergyDepleted(
                elapsedTime,
                300f,
                killCount,
                targetKillCount);

            Assert.That(verdict, Is.EqualTo(expectedVerdict));
        }

        private static string ReadProjectFile(string projectPath)
        {
            return File.ReadAllText(ToAbsolutePath(projectPath));
        }

        private static string ToAbsolutePath(string projectPath)
        {
            return Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                projectPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static void InvokeUpdate(GameManager manager)
        {
            manager.GetType().GetMethod(
                "Update",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.Invoke(manager, null);
        }
    }
}
