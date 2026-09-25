using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 锁定服务是否由当前组合根持有，避免无状态辅助对象和未接入功能进入 VContainer。
    /// </summary>
    public sealed class ServiceCompositionContractTests
    {
        private const string ProjectCoreInstallerPath =
            "Assets/_Project/Scripts/Composition/Installers/Project/ProjectCoreInstaller.cs";
        private const string ProjectLifetimeScopePath =
            "Assets/_Project/Scripts/Composition/Scopes/ProjectLifetimeScope.cs";
        private const string GameLoopCombatInstallerPath =
            "Assets/_Project/Scripts/Composition/Installers/GameLoop/GameLoopCombatInstaller.cs";
        private const string GameLoopEffectInstallerPath =
            "Assets/_Project/Scripts/Composition/Installers/GameLoop/GameLoopEffectInstaller.cs";
        private const string GameLoopRunInstallerPath =
            "Assets/_Project/Scripts/Composition/Installers/GameLoop/GameLoopRunInstaller.cs";
        private const string GameLoopScopePath =
            "Assets/_Project/Scripts/Composition/Scopes/GameLoopLifetimeScope.cs";
        private const string GameManagerPath =
            "Assets/_Project/Scripts/Run/Implement/GameManager.cs";
        private const string AttackServicePath =
            "Assets/_Project/Scripts/CombatSystem/Shoot/Application/TransmitterAttackService.cs";
        private const string TrajectoryDisplayPath =
            "Assets/_Project/Scripts/Play/Transmitter/TransmitterTrajectoryDisplay.cs";

        [Test]
        public void ProjectComposition_DoesNotRegisterUnconsumedServices()
        {
            string installerSource = ReadProjectFile(ProjectCoreInstallerPath);
            string scopeSource = ReadProjectFile(ProjectLifetimeScopePath);

            Assert.That(installerSource, Does.Not.Contain("ProjectileTrajectoryPredictor"));
            Assert.That(scopeSource, Does.Not.Contain("SupplyInstaller"));
            Assert.That(installerSource, Does.Contain("GameObjectPoolService"));
            Assert.That(installerSource, Does.Contain("OptionsService"));
        }

        [Test]
        public void GameLoopInstallers_DoesNotRegisterSingleConsumerHelpers()
        {
            string combatSource = ReadProjectFile(GameLoopCombatInstallerPath);
            string effectSource = ReadProjectFile(GameLoopEffectInstallerPath);
            string runSource = ReadProjectFile(GameLoopRunInstallerPath);

            Assert.That(combatSource, Does.Not.Contain("Register<TransmitterAttackBuildService>"));
            Assert.That(combatSource, Does.Contain("Register<TransmitterAttackService>"));
            Assert.That(effectSource, Does.Not.Contain("Register<UpgradeEffectPlayer>"));
            Assert.That(effectSource, Does.Contain("RegisterInstance(_combatEffectCatalog)"));
            Assert.That(runSource, Does.Not.Contain("Register<RunRuleService>"));
            Assert.That(runSource, Does.Contain("Register<EnergyService>"));
        }

        [Test]
        public void GameLoopScope_DoesNotExposeUnusedProjectileContracts()
        {
            string source = ReadProjectFile(GameLoopScopePath);

            Assert.That(source, Does.Contain("As<ISpatialQueryService>()"));
            Assert.That(source, Does.Not.Contain("As<IProjectileQueryService>()"));
            Assert.That(source, Does.Not.Contain("As<IProjectileSnapshotPublisher>()"));
            Assert.That(source, Does.Not.Contain("As<Services.IPooledEffectService>()"));
            Assert.That(source, Does.Contain("As<Services.IEffectService>()"));
        }

        [Test]
        public void StatelessHelpers_AreExplicitlyComposedAtTheirSingleOwner()
        {
            string gameManagerSource = ReadProjectFile(GameManagerPath);
            string attackSource = ReadProjectFile(AttackServicePath);
            string trajectorySource = ReadProjectFile(TrajectoryDisplayPath);

            Assert.That(gameManagerSource, Does.Not.Contain("[Inject] private RunRuleService"));
            Assert.That(gameManagerSource, Does.Contain("new RunRuleService()"));
            Assert.That(attackSource, Does.Contain("new TransmitterAttackBuildService"));
            Assert.That(attackSource, Does.Contain("ITransmitterAttackModifierSource"));
            Assert.That(trajectorySource, Does.Not.Contain("[Inject] private IProjectileTrajectorySimulationService"));
            Assert.That(trajectorySource, Does.Contain("new ProjectileTrajectoryPredictor()"));
        }

        private static string ReadProjectFile(string relativePath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string absolutePath = Path.Combine(
                projectRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar));
            return File.ReadAllText(absolutePath);
        }
    }
}
