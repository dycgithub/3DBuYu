using System;
using System.IO;
using System.Reflection;
using CombatSystem;
using NUnit.Framework;
using Services;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace _Project.Tests.Editor
{
    /// <summary>
    /// 锁定战斗测试容器的组合注册、模块查询和最小服务依赖。
    /// </summary>
    public sealed class CombatLabCompositionContractTests
    {
        [Test]
        public void SceneCompositions_UseComponentRegistration()
        {
            string[] paths =
            {
                "Assets/_Project/Scripts/Composition/Scene/ProjectComposition.cs",
                "Assets/_Project/Scripts/Composition/Scene/UiSceneComposition.cs",
                "Assets/_Project/Scripts/Composition/Scene/GameSceneComposition.cs",
                "Assets/_Project/Tests/CombatLab/Composition/CombatLabComposition.cs"
            };

            for (int index = 0; index < paths.Length; index++)
            {
                string path = Path.Combine(
                    Directory.GetParent(Application.dataPath).FullName,
                    paths[index].Replace('/', Path.DirectorySeparatorChar));
                string source = File.ReadAllText(path);

                Assert.That(source, Does.Contain("RegisterComponent"), path);
                Assert.That(source, Does.Not.Contain("RegisterInstance"), path);
            }
        }

        [Test]
        public void CombatLabComposition_AllowsMissingOptionalComponents()
        {
            GameObject gameObject = new("CombatLabCompositionContract");
            gameObject.SetActive(false);
            Camera camera = gameObject.AddComponent<Camera>();
            Play.CentralCore centralCore = gameObject.AddComponent<Play.CentralCore>();
            CombatLabComposition composition =
                gameObject.AddComponent<CombatLabComposition>();

            try
            {
                SetField(composition, "_camera", camera);
                SetField(composition, "_centralCore", centralCore);

                var builder = new ContainerBuilder();
                composition.Register(builder);

                using IObjectResolver container = builder.Build();

                Assert.That(container.Resolve<Camera>(), Is.SameAs(camera));
                Assert.That(
                    container.Resolve<Play.CentralCore>(),
                    Is.SameAs(centralCore));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void CombatLabComposition_HasExpectedModules()
        {
            string[] names = Enum.GetNames(typeof(CombatLabComposition.CombatLabModuleType));

            Assert.That(
                names,
                Is.EqualTo(new[]
                {
                    "Projectile",
                    "Damage",
                    "Ability",
                    "Buff",
                    "Enemy",
                    "Spatial",
                    "Effects"
                }));
        }

        [Test]
        public void CombatLabComposition_ModuleQueryMatchesConfiguration()
        {
            GameObject gameObject = new("CombatLabModuleContract");
            CombatLabComposition composition =
                gameObject.AddComponent<CombatLabComposition>();

            try
            {
                Assert.That(
                    composition.HasModule(
                        CombatLabComposition.CombatLabModuleType.Projectile),
                    Is.False);

                SetField(
                    composition,
                    "_modules",
                    new[] { CombatLabComposition.CombatLabModuleType.Projectile });

                Assert.That(
                    composition.HasModule(
                        CombatLabComposition.CombatLabModuleType.Projectile),
                    Is.True);
                Assert.That(
                    composition.HasModule(
                        CombatLabComposition.CombatLabModuleType.Damage),
                    Is.False);
                Assert.That(
                    composition.HasModule(
                        CombatLabComposition.CombatLabModuleType.Effects),
                    Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        /// <summary>Projectile 缺少空间查询组件时拒绝注册。</summary>
        [Test]
        public void ProjectileNeedsSpatialSource()
        {
            GameObject gameObject = new("CombatLabSpatialSourceContract");
            gameObject.SetActive(false);
            Camera camera = gameObject.AddComponent<Camera>();
            Play.CentralCore centralCore = gameObject.AddComponent<Play.CentralCore>();
            EffectSystem.EffectManager effectManager =
                gameObject.AddComponent<EffectSystem.EffectManager>();
            CombatLabComposition composition =
                gameObject.AddComponent<CombatLabComposition>();

            try
            {
                SetField(composition, "_camera", camera);
                SetField(composition, "_centralCore", centralCore);
                SetField(composition, "_effectManager", effectManager);
                SetField(
                    composition,
                    "_modules",
                    new[] { CombatLabComposition.CombatLabModuleType.Projectile });

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => composition.Register(new ContainerBuilder()));

                Assert.That(exception.Message, Does.Contain("ISpatialQueryService"));
                Assert.That(exception.Message, Does.Contain("SpatialRegistry"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        /// <summary>Projectile 缺少特效管理器时拒绝注册。</summary>
        [Test]
        public void ProjectileNeedsEffectSource()
        {
            GameObject gameObject = new("CombatLabEffectSourceContract");
            gameObject.SetActive(false);
            Camera camera = gameObject.AddComponent<Camera>();
            Play.CentralCore centralCore = gameObject.AddComponent<Play.CentralCore>();
            SpatialSystem.Bridge.SpatialRegistry spatialRegistry =
                gameObject.AddComponent<SpatialSystem.Bridge.SpatialRegistry>();
            CombatLabComposition composition =
                gameObject.AddComponent<CombatLabComposition>();

            try
            {
                SetField(composition, "_camera", camera);
                SetField(composition, "_centralCore", centralCore);
                SetField(composition, "_spatialRegistry", spatialRegistry);
                SetField(
                    composition,
                    "_modules",
                    new[]
                    {
                        CombatLabComposition.CombatLabModuleType.Projectile,
                        CombatLabComposition.CombatLabModuleType.Effects
                    });

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => composition.Register(new ContainerBuilder()));

                Assert.That(exception.Message, Does.Contain("IEffectService"));
                Assert.That(exception.Message, Does.Contain("EffectManager"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        /// <summary>Effects 缺少特效目录时在注册阶段拒绝构建组合。</summary>
        [Test]
        public void EffectsNeedCatalog()
        {
            GameObject gameObject = new("CombatLabEffectsContract");
            CombatLabComposition composition =
                gameObject.AddComponent<CombatLabComposition>();

            try
            {
                SetField(
                    composition,
                    "_modules",
                    new[] { CombatLabComposition.CombatLabModuleType.Effects });
                CombatLabInstaller installer = new(composition, null, null, gameObject);

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => installer.Install(new ContainerBuilder()));

                Assert.That(exception.Message, Does.Contain("Effects"));
                Assert.That(exception.Message, Does.Contain("CombatEffectCatalogSO"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        /// <summary>Enemy 缺少群游配置时保留 GameLoop 的错误语义。</summary>
        [Test]
        public void EnemyNeedsFlockSettings()
        {
            GameObject gameObject = new("CombatLabEnemyContract");
            CombatLabComposition composition =
                gameObject.AddComponent<CombatLabComposition>();

            try
            {
                SetField(
                    composition,
                    "_modules",
                    new[] { CombatLabComposition.CombatLabModuleType.Enemy });
                CombatLabInstaller installer = new(composition, null, null, gameObject);

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => installer.Install(new ContainerBuilder()));

                Assert.That(
                    exception.Message,
                    Is.EqualTo(
                        "[GameLoopLifetimeScope] CombatLabEnemyContract: 未配置 EnemyFlockSettings，无法启动 ECS Flocking。"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void CombatLabPhase_DefaultAllowsCombat()
        {
            var phase = new CombatLabPhaseService();

            Assert.That(phase.CanPerformCombatActions, Is.True);
        }

        [Test]
        public void CombatLabScope_ResolvesMinimalCombatServices()
        {
            GameObject gameObject = new("CombatLabScopeContract");
            gameObject.SetActive(false);
            Camera camera = gameObject.AddComponent<Camera>();
            Play.CentralCore centralCore = gameObject.AddComponent<Play.CentralCore>();
            EffectSystem.EffectManager effectManager =
                gameObject.AddComponent<EffectSystem.EffectManager>();
            SpatialSystem.Bridge.SpatialRegistry spatialRegistry =
                gameObject.AddComponent<SpatialSystem.Bridge.SpatialRegistry>();
            CombatLabComposition composition =
                gameObject.AddComponent<CombatLabComposition>();
            CombatLabLifetimeScope scope =
                gameObject.AddComponent<CombatLabLifetimeScope>();
            EffectSystem.CombatEffectCatalogSO catalog =
                ScriptableObject.CreateInstance<EffectSystem.CombatEffectCatalogSO>();

            try
            {
                SetField(composition, "_camera", camera);
                SetField(composition, "_centralCore", centralCore);
                SetField(composition, "_effectManager", effectManager);
                SetField(composition, "_spatialRegistry", spatialRegistry);
                SetField(
                    composition,
                    "_modules",
                    new[]
                    {
                        CombatLabComposition.CombatLabModuleType.Projectile,
                        CombatLabComposition.CombatLabModuleType.Damage,
                        CombatLabComposition.CombatLabModuleType.Ability,
                        CombatLabComposition.CombatLabModuleType.Spatial,
                        CombatLabComposition.CombatLabModuleType.Effects
                    });
                SetField(scope, "_composition", composition);
                SetField(scope, "_combatEffectCatalog", catalog);

                scope.autoRun = false;
                using (LifetimeScope.EnqueueParent(null))
                {
                    scope.Build();
                }

                Assert.That(
                    scope.Container.Resolve<ICombatPhaseService>(),
                    Is.TypeOf<CombatLabPhaseService>());
                Assert.That(scope.Container.Resolve<AttackExecutor>(), Is.Not.Null);
                Assert.That(
                    scope.Container.Resolve<ITransmitterAttackService>(),
                    Is.Not.Null);
                Assert.That(
                    scope.Container.Resolve<ISkillExecutionService>(),
                    Is.Not.Null);
                Assert.That(
                    scope.Container.Resolve<ICentralSkillEffectService>(),
                    Is.Not.Null);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(target, value);
        }
    }
}
