using CombatSystem;
using Services;
using Utils;
using VContainer;
using VContainer.Unity;

/// <summary>
/// 注册 ProjectLifetimeScope 中不依赖场景查找的全局核心服务。
/// 所有服务均为根容器单例；场景组件仍由调用方按原语义注册。
/// </summary>
public sealed class ProjectCoreInstaller : IInstaller
{
    /// <summary>
    /// 注册全局通知、对象池、积分表现和弹道预测服务。
    /// </summary>
    /// <param name="builder">ProjectLifetimeScope 使用的容器构建器。</param>
    public void Install(IContainerBuilder builder)
    {
        builder.Register<_Project.UI.Common.UINotificationService>(Lifetime.Singleton)
            .As<Services.IUINotificationService>()
            .AsSelf();
        builder.Register<GameObjectPoolService>(Lifetime.Singleton)
            .As<IGameObjectPool>()
            .AsSelf();
        builder.Register<PointsFlyEffectService>(Lifetime.Singleton)
            .As<IPointsFlyEffectService>()
            .AsSelf();
        builder.Register<ProjectileTrajectoryPredictor>(Lifetime.Singleton)
            .As<IProjectileTrajectorySimulationService>()
            .AsSelf();
    }
}
