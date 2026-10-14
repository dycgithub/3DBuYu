using CombatSystem;
using EffectSystem;
using GameSystem;
using VContainer;
using VContainer.Unity;

/// <summary>
/// GameLoop 战斗编排段：注册伤害、攻击身份、Buff、发射器表现与技能服务。
/// 所有服务均为当前 GameLoop 子容器单例。
/// </summary>
public sealed class GameLoopCombatInstaller : IInstaller
{
    /// <summary>
    /// 注册共享战斗服务、发射器攻击、战斗消费、技能效果、技能执行和技能激活服务。
    /// </summary>
    /// <param name="builder">GameLoopLifetimeScope 使用的容器构建器。</param>
    public void Install(IContainerBuilder builder)
    {
        builder.Register<DamageRequestProcessor>(Lifetime.Singleton)
            .As<IDamageRequestProcessor>()
            .As<IDamageEventSource>()
            .AsSelf();
        builder.Register<AttackInstanceIdGenerator>(Lifetime.Singleton)
            .As<IAttackInstanceIdGenerator>()
            .AsSelf();
        builder.Register<UnityAttackClock>(Lifetime.Singleton).As<IAttackClock>();
        builder.Register<AttackCooldownRegistry>(Lifetime.Singleton).AsSelf();
        builder.Register<AttackDataBuilder>(Lifetime.Singleton).AsSelf();
        builder.Register<TransmitterAttackBuildService>(Lifetime.Singleton).AsSelf();
        builder.Register<AttackExecutor>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<BuffService>(Lifetime.Singleton)
            .As<IBuffService>()
            .AsSelf();
        builder.Register<TransmitterAttackService>(Lifetime.Singleton)
            .As<ITransmitterAttackService>()
            .AsSelf();
        builder.RegisterEntryPoint<TransmitterFireAudio>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<ProjectileHitAudio>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<TransmitterFireEffect>(Lifetime.Singleton).AsSelf();
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
