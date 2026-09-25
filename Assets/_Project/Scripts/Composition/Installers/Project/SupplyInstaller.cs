using Supply;
using VContainer;
using VContainer.Unity;

/// <summary>注册跨 UI 场景共享的 Supply 核心服务。</summary>
public sealed class SupplyInstaller : IInstaller
{
    /// <summary>注册物品池、随机源和三种抽奖服务。</summary>
    /// <param name="builder">ProjectLifetimeScope 使用的容器构建器。</param>
    public void Install(IContainerBuilder builder)
    {
        builder.Register<UnityRandomSource>(Lifetime.Singleton)
            .As<IRandomSource>()
            .AsSelf();
        builder.Register<SupplyItemPool>(
                container => SupplyItemPoolFactory.FromCatalog(container.Resolve<ItemCatalog>()),
                Lifetime.Singleton)
            .AsSelf();
        builder.Register<ChestRules>(
                _ => ChestRules.CreateDefault(),
                Lifetime.Singleton)
            .AsSelf();
        builder.Register<SlotMachineService>(Lifetime.Singleton).AsSelf();
        builder.Register<ChestOpeningService>(Lifetime.Singleton).AsSelf();
        builder.Register<MobiusPoolService>(Lifetime.Singleton).AsSelf();
    }
}
