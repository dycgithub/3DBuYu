using CombatSystem;
using VContainer;
using VContainer.Unity;

/// <summary>
/// 注册 ProjectLifetimeScope 中跨场景共享的战斗状态与装备服务。
/// 所有服务均为根容器单例。
/// </summary>
public sealed class ProjectCombatInstaller : IInstaller
{
    /// <summary>
    /// 注册全局战斗定义解析器、技能手牌和发射器装备状态。
    /// </summary>
    /// <param name="builder">ProjectLifetimeScope 使用的容器构建器。</param>
    public void Install(IContainerBuilder builder)
    {
        builder.Register<ItemCombatDefinitionResolver>(Lifetime.Singleton).AsSelf();
        builder.Register<SkillHand>(Lifetime.Singleton).AsSelf();
        builder.Register<CentralSkillLoadout>(Lifetime.Singleton).AsSelf();
        builder.Register<TransmitterGlobalEffectService>(Lifetime.Singleton)
            .As<ITransmitterGlobalEffectService>()
            .As<ITransmitterGlobalEffectResetService>()
            .AsSelf();
        builder.Register<TransmitterAttackLoadout>(Lifetime.Singleton)
            .As<ITransmitterAttackModifierSource>()
            .AsSelf();
    }
}
