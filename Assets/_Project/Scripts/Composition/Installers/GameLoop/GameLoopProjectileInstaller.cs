using CombatSystem;
using VContainer;
using VContainer.Unity;

/// <summary>
/// GameLoop 投射物段：注册集中式投射物模拟管线。
/// 所有服务和对象池均为当前 GameLoop 子容器单例。
/// </summary>
public sealed class GameLoopProjectileInstaller : IInstaller
{
    /// <summary>
    /// 注册投射物池、碰撞查询、发射和模拟服务。
    /// </summary>
    /// <param name="builder">GameLoopLifetimeScope 使用的容器构建器。</param>
    public void Install(IContainerBuilder builder)
    {
        builder.Register<ProjectileViewPool>(Lifetime.Singleton).AsSelf();
        builder.Register<ProjectileStatePool>(Lifetime.Singleton).AsSelf();
        builder.Register<ProjectileCollisionQuery>(Lifetime.Singleton)
            .As<IProjectileCollisionQuery>()
            .AsSelf();
        builder.Register<ProjectileEffectDispatcher>(Lifetime.Singleton)
            .As<IProjectileEffectDispatcher>()
            .AsSelf();
        builder.Register<ProjectileLauncher>(Lifetime.Singleton)
            .As<IProjectileLauncher>()
            .AsSelf();
        builder.RegisterEntryPoint<ProjectileSimulationService>(Lifetime.Singleton)
            .As<IProjectileHitEventSource>()
            .AsSelf();
        builder.Register<ProjectileSimulationSpawner>(Lifetime.Singleton)
            .As<IProjectileSpawner>()
            .AsSelf();
    }
}
