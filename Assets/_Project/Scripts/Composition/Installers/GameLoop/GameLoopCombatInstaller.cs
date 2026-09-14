using CombatSystem;
using VContainer;
using VContainer.Unity;

/// <summary>
/// GameLoop 战斗编排段：在投射物管线之后注册发射器与技能服务。
/// 所有服务均为当前 GameLoop 子容器单例。
/// </summary>
public sealed class GameLoopCombatInstaller : IInstaller
{
    /// <summary>
    /// 注册装备构建、战斗消费、技能效果、技能执行和技能激活服务。
    /// </summary>
    /// <param name="builder">GameLoopLifetimeScope 使用的容器构建器。</param>
    public void Install(IContainerBuilder builder)
    {
        builder.Register<TransmitterAttackBuildService>(Lifetime.Singleton).AsSelf();
        builder.Register<TransmitterAttackService>(Lifetime.Singleton)
            .As<ITransmitterAttackService>()
            .AsSelf();
        builder.RegisterEntryPoint<CombatLoadoutInventoryBinder>(Lifetime.Singleton)
            .As<ICombatItemConsumptionService>()
            .AsSelf();
        builder.Register<CentralSkillEffectService>(Lifetime.Singleton)
            .As<ICentralSkillEffectService>()
            .As<ICentralSkillEffectResetService>()
            .AsSelf();
        builder.Register<SkillExecutor>(Lifetime.Singleton)
            .As<ISkillExecutionService>()
            .AsSelf();
        builder.RegisterEntryPoint<SkillActivationService>(Lifetime.Singleton).AsSelf();
    }
}
