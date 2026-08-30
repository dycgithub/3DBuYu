using GameSystem;
using _Project.UI.Common;
using VContainer;
using VContainer.Unity;
using CombatSystem;
using Services;
using CameraSystem;
using SpatialSystem.Bridge;
using EnemySystem.Spawning;
using EnemySystem.Wave;
using FlockingSystem.ECS;
using Play;
using UnityEngine;
using Utils;
using EffectSystem;

/// <summary>
/// GameLoopScene 的场景子容器,随场景卸载而销毁。
/// 父容器为 ProjectLifetimeScope(全局,永不销毁);本容器只注册场景内组件。
/// </summary>
public class GameLoopLifetimeScope : LifetimeScope
{
    [Header("Item Tooltip")]
    [SerializeField] private bool hoverTooltipEnabled = true;
    [SerializeField] private bool selectedTooltipEnabled = false;
    [Header("Effects")]
    [SerializeField] private CombatEffectCatalogSO combatEffectCatalog;
    [Header("Flocking")]
    [Tooltip("ECS Flocking 的场景级配置资产。")]
    [SerializeField] private EnemyFlockSettingsSO enemyFlockSettings;

    protected override void Configure(IContainerBuilder builder)
    {
        // === 场景组件(随 GameLoopScene 卸载销毁) ===
        // 注册顺序很重要:被注入的依赖必须先注册
        builder.RegisterEntryPoint<InventoryManager>(Lifetime.Singleton)
            .As<IInventoryDragState>()
            .As<IInventorySelectionState>()
            .AsSelf();
        builder.Register<ItemTooltipManager>(Lifetime.Singleton)
            .As<IItemTooltipService>()
            .AsSelf()
            .WithParameter<ItemTooltipMode>(ItemTooltipMode.Moving)
            .WithParameter("hoverEnabled", hoverTooltipEnabled)
            .WithParameter("selectedEnabled", selectedTooltipEnabled);
        builder.RegisterComponentInHierarchy<InputSystem.InputService>().As<IInputService>().AsSelf();
        builder.RegisterComponentInHierarchy<WaveController>().As<IWaveEventService>().AsSelf();
        builder.RegisterComponentInHierarchy<SpawnPositionProvider>().As<ISpawnPositionProvider>().AsSelf();
        builder.RegisterComponentInHierarchy<SpatialRegistry>().As<ISpatialQueryService>().AsSelf();
        if (enemyFlockSettings != null)
        {
            builder.RegisterInstance(enemyFlockSettings).AsSelf();
            builder.Register<EnemyFlockRuntimeService>(Lifetime.Singleton).AsSelf();
        }
        else
        {
            Debug.LogError("[GameLoopLifetimeScope] 未配置 EnemyFlockSettings，无法启动 ECS Flocking。", this);
        }

        if (combatEffectCatalog != null)
            builder.RegisterInstance(combatEffectCatalog).AsSelf();
        else
            Debug.LogError("[GameLoopLifetimeScope] 未配置 CombatEffectCatalogSO。", this);
        builder.Register<RunRuleService>(Lifetime.Singleton).AsSelf();

        // Per-run combat resources must be disposed with the game-loop scope.
        builder.Register<EnergyService>(Lifetime.Singleton).As<IEnergyService>().AsSelf();
        builder.Register<KillStreakService>(Lifetime.Singleton).As<IKillStreakService>().AsSelf();
        builder.Register<GamePauseService>(Lifetime.Singleton).As<IGamePauseService>().AsSelf();
        builder.Register<GameObjectPoolService>(Lifetime.Singleton).As<IGameObjectPool>().AsSelf();

        // === 战斗期射击服务(随本局场景销毁) ===
        // 子弹逻辑使用集中式模拟器和对象池；ECS 仅保留 Flocking。
        builder.Register<ProjectilePool>(Lifetime.Singleton).AsSelf();
        builder.Register<ProjectileRuntimePool>(Lifetime.Singleton).AsSelf();
        builder.Register<ProjectileHitQuery>(Lifetime.Singleton)
            .As<IProjectileHitQuery>()
            .AsSelf();
        builder.Register<DamagePipeline>(Lifetime.Singleton)
            .As<IDamageApplier>()
            .AsSelf();
        builder.Register<BulletEffectDispatcher>(Lifetime.Singleton)
            .As<IBulletEffectDispatcher>()
            .AsSelf();
        builder.Register<UnityAttackClock>(Lifetime.Singleton).As<IAttackClock>();
        builder.Register<AttackCooldownRegistry>(Lifetime.Singleton).AsSelf();
        builder.Register<AttackBuilder>(Lifetime.Singleton).AsSelf();
        builder.Register<AttackExecutor>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<ProjectileSimulationService>(Lifetime.Singleton).AsSelf();
        builder.Register<PooledBulletSpawner>(Lifetime.Singleton).As<IProjectileSpawner>().AsSelf();
        builder.Register<TransmitterShootBuildService>(Lifetime.Singleton).AsSelf();
        builder.Register<TransmitterAttackService>(Lifetime.Singleton)
            .As<ITransmitterAttackService>()
            .AsSelf();
        builder.RegisterEntryPoint<AbilityService>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<CombatInventoryBinder>(Lifetime.Singleton)
            .As<ICombatItemConsumer>()
            .AsSelf();

        // === 敌人(纯 C# 服务,依赖上面的场景组件,故放子容器) ===
        builder.Register<EnemyPool>(Lifetime.Singleton).AsSelf();
        builder.Register<EnemyFactory>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<EnemySpawner>(Lifetime.Singleton)
            .As<IEnemySpawner>()
            .AsSelf();

        // 依赖以上服务的核心系统
        builder.RegisterComponentInHierarchy<GameManager>()
            .As<Services.IGameEventService>()
            .As<Services.ICombatPhaseService>()
            .AsSelf();
        builder.RegisterComponentInHierarchy<SphericalCameraDirector>().AsSelf();
        builder.RegisterComponentInHierarchy<EffectSystem.EffectManager>()
            .As<Services.IEffectService>()
            .As<Services.IPooledEffectService>()
            .AsSelf();
        builder.RegisterComponentInHierarchy<AudioManager>().AsSelf();
        builder.Register<EffectSystem.UpgradeEffectPlayer>(Lifetime.Singleton).AsSelf();

        // === 场景组件 ===
        builder.RegisterComponentInHierarchy<CentralCore>();
        builder.RegisterComponentInHierarchy<TransmitterFireController>();
        builder.RegisterComponentInHierarchy<PhysicsCentralDetector>();
        builder.RegisterComponentInHierarchy<PlayerController>();
        builder.RegisterComponentInHierarchy<StaminaController>();
        builder.RegisterComponentInHierarchy<CollisionTimePenalty>();

        // UI 场景组件由各自场景 Scope 的 autoInjectGameObjects 负责注入。
    }
}
