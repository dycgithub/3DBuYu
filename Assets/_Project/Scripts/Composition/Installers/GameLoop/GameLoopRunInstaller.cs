using GameSystem;
using Services;
using VContainer;
using VContainer.Unity;

/// <summary>
/// GameLoop 本局服务段：注册本局规则、资源、暂停和音频暂停绑定。
/// 所有服务均为当前 GameLoop 子容器单例；群游配置由 Enemy Installer 提前建立。
/// </summary>
public sealed class GameLoopRunInstaller : IInstaller
{
    /// <summary>
    /// 注册本局规则、能量、连杀、暂停和暂停音频绑定。
    /// </summary>
    /// <param name="builder">GameLoopLifetimeScope 使用的容器构建器。</param>
    public void Install(IContainerBuilder builder)
    {
        builder.Register<RunRuleService>(Lifetime.Singleton).AsSelf();
        builder.Register<EnergyService>(Lifetime.Singleton)
            .As<IEnergyService>()
            .AsSelf();
        builder.Register<KillStreakService>(Lifetime.Singleton)
            .As<IKillStreakService>()
            .AsSelf();
        builder.Register<GamePauseService>(Lifetime.Singleton)
            .As<IGamePauseService>()
            .AsSelf();
        builder.RegisterEntryPoint<AudioPauseBinding>(Lifetime.Singleton).AsSelf();
    }
}
