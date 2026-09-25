using GameSystem;
using VContainer;
using VContainer.Unity;
using CombatSystem;
using Services;
using CameraSystem;
using SpatialIndexing;
using EnemySystem.Spawning;
using EnemySystem;
using FlockingSystem.ECS;
using Interfaces;
using Play;
using Play.CombatVisualization.View;
using UnityEngine;
using EffectSystem;
using _Project.UI.Card;
using _Project.UI.Settings;
using _Project.UI.RunResult.View;
using _Project.UI.Damage;

/// <summary>
/// GameLoopScene 的场景子容器,随场景卸载而销毁。
/// 父容器为 ProjectLifetimeScope(全局,永不销毁);本容器只注册场景内组件,全局对象池从父容器解析。
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
    [Header("Damage Numbers")]
    [Tooltip("伤害跳字的池和动画配置；为空时使用代码默认值。")]
    [SerializeField] private DamageNumberConfigSO damageNumberConfig;

    /// <summary>
    /// 注册战斗场景范围内的服务、组件和对象池。
    /// </summary>
    protected override void Configure(IContainerBuilder builder)
    {
        // === 场景组件(随 GameLoopScene 卸载销毁) ===
        // 注册顺序很重要:被注入的依赖必须先注册
        new GameLoopInventoryInstaller(new ItemTooltipConfiguration
        {
            HoverEnabled = hoverTooltipEnabled,
            SelectedEnabled = selectedTooltipEnabled,
        }).Install(builder);
        // Input 节点属于 GameScene 子容器，玩家控制器和 GameManager 都依赖该接口。
        builder.RegisterComponentInHierarchy<InputSystem.InputService>()
            .As<IInputService>()
            .AsSelf();
        builder.RegisterComponentInHierarchy<EnemyPopulationController>().As<IEnemyPopulationService>().AsSelf();
        builder.RegisterComponentInHierarchy<SpawnPositionProvider>().As<ISpawnPositionProvider>().AsSelf();
        builder.Register<SpatialQueryService>(Lifetime.Scoped)
            .As<ISpatialQueryService>()
            .AsSelf();
        new GameLoopEnemyInstaller(enemyFlockSettings, name).Install(builder);
        new GameLoopEffectInstaller(combatEffectCatalog, this).Install(builder);

        // 本局资源随 GameLoop 子容器释放;全局对象池由父容器唯一持有,避免重复注册。
        new GameLoopRunInstaller().Install(builder);

        // === 战斗期射击服务(随本局场景销毁) ===
        // 子弹逻辑使用集中式模拟器和对象池；ECS 仅保留 Flocking。
        new GameLoopProjectileInstaller().Install(builder);
        if (damageNumberConfig != null)
        {
            builder.RegisterInstance(damageNumberConfig).AsSelf();
        }

        builder.RegisterComponentInHierarchy<DamageNumberLayer>().AsSelf();
        builder.RegisterEntryPoint<DamageNumberSystem>(Lifetime.Singleton).AsSelf();
        // 直接发射入口不处理能量、冷却和阶段，玩家规则服务与敌人组件共用同一投射物管线。
        // 直接技能入口不处理玩家专属前置，玩家释放服务和敌人能力组件共用动作执行器。
        new GameLoopCombatInstaller().Install(builder);
        builder.RegisterComponentInHierarchy<TransmitterBackpackBinder>().AsSelf();
        builder.RegisterComponentInHierarchy<GameintStatisticsUIToolkit>().AsSelf();
        builder.RegisterComponentInHierarchy<RunResultPanel>().AsSelf();
        builder.RegisterComponentInHierarchy<DifficultySelector>().AsSelf();

        // 依赖以上服务的核心系统
        builder.RegisterComponentInHierarchy<GameManager>()
            .As<Services.IGameEventService>()
            .As<Services.ICombatPhaseService>()
            .AsSelf();
        builder.RegisterComponentInHierarchy<SphericalCameraFollow>().AsSelf();
        builder.RegisterComponentInHierarchy<SphericalCameraRegional>().AsSelf();
        builder.RegisterComponentInHierarchy<SphericalCameraDirector>().AsSelf();
        builder.RegisterComponentInHierarchy<EffectSystem.EffectManager>()
            .As<Services.IEffectService>()
            .AsSelf();

        // === 场景组件 ===
        // 移动方向使用当前 GameLoop 场景相机，并由容器注入 PlayerController。
        builder.RegisterComponentInHierarchy<Camera>().AsSelf();
        builder.RegisterComponentInHierarchy<CentralCore>();
        builder.RegisterComponentInHierarchy<TransmitterFireController>();
        builder.RegisterComponentInHierarchy<PhysicsCentralDetector>();
        builder.RegisterComponentInHierarchy<CombatRangeVisualization>();
        builder.RegisterComponentInHierarchy<PlayerController>();
        builder.RegisterComponentInHierarchy<PlayerCombatTarget>()
            .As<IPlayerCombatTarget>()
            .AsSelf();
        // 轨道通过数量事件绑定，静止玩家也会随实际存活数调整半径。
        builder.RegisterComponentInHierarchy<SphereWalker>();
        builder.RegisterComponentInHierarchy<StaminaController>();
        builder.RegisterComponentInHierarchy<PlayerCollisionFeedback>();
        builder.RegisterComponentInHierarchy<CardHandView>();
        // 以上 UI 组件随 GameLoop 场景创建，并由当前子容器注入。
    }
}
