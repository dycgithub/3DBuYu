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
using Play.FieldContact.View;
using UnityEngine;
using EffectSystem;
using _Project.UI.Card;
using _Project.UI.Settings;
using _Project.UI.RunResult.View;
using _Project.UI.Damage;
using _Project.UI.EnemyThreatMarker;
using _Project.UI.MousePointer;
using _Project.UI.Supply;
using Supply;
using Play.TargetMark;

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
    [Header("Enemy Population")]
    [Tooltip("所有敌人分类共享的全局存活容量。")]
    [SerializeField] private EnemyPopulationSettingsSO enemyPopulationSettings;
    [Header("Damage Numbers")]
    [Tooltip("伤害跳字的池和动画配置；为空时使用代码默认值。")]
    [SerializeField] private DamageNumberConfigSO damageNumberConfig;
    [Header("Supply Entry")]
    [SerializeField] private SupplyEntryCostsSO supplyEntryCosts;

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
        new GameLoopEnemyInstaller(enemyFlockSettings, enemyPopulationSettings, name).Install(builder);
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
        // 接触威胁标记复用场景 Overlay Canvas 与全局图片对象池。
        builder.RegisterComponentInHierarchy<EnemyThreatMarkerLayer>().AsSelf();
        builder.RegisterEntryPoint<EnemyThreatMarkerSystem>(Lifetime.Singleton).AsSelf();
        // SupplyScene 覆盖期间 GameScene Canvas 停用，鼠标准星与痕迹同步隐藏。
        builder.RegisterComponentInHierarchy<MousePointerGraphic>().AsSelf();
        // 补给面板在本局场景内直接抽取并交付到普通背包；旧入口服务暂供已有场景逻辑使用。
        builder.Register<SupplyFiveDrawService>(Lifetime.Scoped).AsSelf();
        builder.RegisterComponentInHierarchy<SupplyPanel>().AsSelf();
        // 入场费用配置可选；未引用资源时服务使用代码默认金额。
        builder.Register<SupplyEntryService>(
                container => new SupplyEntryService(
                    container.Resolve<IPointsService>(),
                    supplyEntryCosts),
                Lifetime.Scoped)
            .AsSelf();
        // 直接发射入口不处理能量、冷却和阶段，玩家规则服务与敌人组件共用同一投射物管线。
        // 直接技能入口不处理玩家专属前置，玩家释放服务和敌人能力组件共用动作执行器。
        new GameLoopCombatInstaller().Install(builder);
        builder.RegisterComponentInHierarchy<TransmitterBackpackBinder>().AsSelf();
        builder.RegisterComponentInHierarchy<GameintStatisticsUIToolkit>().AsSelf();
        builder.RegisterComponentInHierarchy<RunResultPanel>().AsSelf();
        builder.RegisterComponentInHierarchy<DifficultySelector>().AsSelf();
        builder.RegisterComponentInHierarchy<SupplySceneEntryController>().AsSelf();

        // 依赖以上服务的核心系统
        builder.RegisterComponentInHierarchy<GameManager>()
            .As<Services.IGameEventService>()
            .As<Services.ICombatPhaseService>()
            .AsSelf();
        // 标记队列在死亡、结算和场景卸载时释放，并把队首同步到攻击目标服务。
        builder.Register<MarkedTargetService>(Lifetime.Scoped).AsSelf();
        builder.RegisterEntryPoint<MarkedTargetQueueService>(Lifetime.Scoped).AsSelf();
        builder.RegisterComponentInHierarchy<SphericalCameraFollow>().AsSelf();
        builder.RegisterComponentInHierarchy<SphericalCameraRegional>().AsSelf();
        builder.RegisterComponentInHierarchy<SphericalCameraDirector>()
            .As<ICameraMovementBasis>()
            .AsSelf();
        // Buff 持续视效按实例归还，瞬时战斗特效仍通过同一管理器播放。
        builder.RegisterComponentInHierarchy<EffectSystem.EffectManager>()
            .As<Services.IEffectService>()
            .As<Services.IPooledEffectService>()
            .AsSelf();

        // === 场景组件 ===
        // 输出 Camera 保留给需要读取实际渲染姿态的场景组件；玩家移动使用逻辑切线基底。
        builder.RegisterComponentInHierarchy<Camera>().AsSelf();
        builder.RegisterComponentInHierarchy<CentralCore>();
        builder.RegisterComponentInHierarchy<TransmitterFireController>();
        // 注入发射器瞄准控制器使用的攻击模式与玩家输入服务。
        builder.RegisterComponentInHierarchy<TransmitterAimController>();
        builder.RegisterComponentInHierarchy<PhysicsCentralDetector>();
        // 标记组件需要相机、中央核心与物理探测器，须在这些场景依赖注册后加入容器。
        builder.RegisterComponentInHierarchy<MarkedTargetInputController>().AsSelf();
        builder.RegisterComponentInHierarchy<MarkedTargetMarkerView>().AsSelf();
        builder.RegisterComponentInHierarchy<FieldContactView>();
        builder.RegisterComponentInHierarchy<PlayerController>();
        builder.RegisterComponentInHierarchy<PlayerCombatTarget>()
            .As<IPlayerCombatTarget>()
            .AsSelf();
        // 轨道通过数量事件绑定，静止玩家也会随实际存活数调整半径。
        builder.RegisterComponentInHierarchy<SphereWalker>();
        builder.RegisterComponentInHierarchy<StaminaController>();
        builder.RegisterComponentInHierarchy<PlayerCollisionFeedback>();
        builder.RegisterComponentInHierarchy<CardHandView>();
        // 同一门控汇总设置、背包和补给面板的攻击输入状态。
        builder.RegisterComponentInHierarchy<PanelAttackInputGate>().AsSelf();
        // 以上 UI 组件随 GameLoop 场景创建，并由当前子容器注入。
    }
}
