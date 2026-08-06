using GameSystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using ShootingSystem;
using ShootingSystem.Bullets.ECS.Systems;
using Services;
using SpatialSystem.Bridge;
using EnemySystem.Spawning;
using EnemySystem.Wave;
using FlockingSystem;

/// <summary>
/// GameLoopScene 的场景子容器,随场景卸载而销毁。
/// 父容器为 ProjectLifetimeScope(全局,永不销毁);本容器只注册场景内组件。
/// </summary>
public class GameLoopLifetimeScope : LifetimeScope
{
    protected override LifetimeScope FindParent()
    {
        return ProjectLifetimeScope.Instance;
    }

    protected override void Configure(IContainerBuilder builder)
    {
        // === 场景组件(随 GameLoopScene 卸载销毁) ===
        // 注册顺序很重要:被注入的依赖必须先注册
        builder.RegisterComponentInHierarchy<InputSystem.InputService>().As<IInputService>().AsSelf();
        builder.RegisterComponentInHierarchy<WaveController>().As<IWaveEventService>().AsSelf();
        builder.RegisterComponentInHierarchy<SpawnPositionProvider>().As<ISpawnPositionProvider>().AsSelf();
        builder.RegisterComponentInHierarchy<SpatialRegistry>().As<ISpatialQueryService>().AsSelf();
        builder.RegisterComponentInHierarchy<FlockManager>().AsSelf();

        // === 敌人(纯 C# 服务,依赖上面的场景组件,故放子容器) ===
        builder.Register<EnemyPool>(Lifetime.Singleton).AsSelf();
        builder.Register<EnemyFactory>(Lifetime.Singleton).AsSelf();
        builder.Register<EnemySpawner>(Lifetime.Singleton).As<IEnemySpawner>().AsSelf();

        // === 主动技能(命令模式):Receiver + Invoker,依赖 Turret/敌人/装备,须在 GameManager 前注册 ===
        builder.Register<ItemSystem.Functions.SkillActivationContext>(Lifetime.Singleton)
            .As<ItemSystem.Functions.IItemActivationContext>().AsSelf();
        builder.Register<ItemSystem.Functions.SkillManager>(Lifetime.Singleton).AsSelf();

        // 依赖以上服务的核心系统
        builder.RegisterComponentInHierarchy<GameManager>().As<Services.IGameEventService>().AsSelf();
        builder.RegisterComponentInHierarchy<EffectSystem.EffectManager>().As<Services.IEffectService>().AsSelf();
        builder.RegisterComponentInHierarchy<InventorySystem.DurabilityManager>().AsSelf();
        builder.RegisterComponentInHierarchy<AudioManager>().AsSelf();

        // === 场景组件 ===
        builder.RegisterComponentInHierarchy<ShootingSystemBootstrap>();
        builder.RegisterComponentInHierarchy<TurretSystem.Turret>();
        builder.RegisterComponentInHierarchy<TurretSystem.PortFireController>();
        builder.RegisterComponentInHierarchy<TurretSystem.PhysicsTurretDetector>();
        builder.RegisterComponentInHierarchy<PlayerController>();

        // === UI 组件(依赖注入,故注册) ===
        builder.RegisterComponentInHierarchy<_Project.UI.Common.UINotificationView>().AsSelf();
        builder.RegisterComponentInHierarchy<_Project.UI.Settings.SettingsPanel>().AsSelf();
    }
}
