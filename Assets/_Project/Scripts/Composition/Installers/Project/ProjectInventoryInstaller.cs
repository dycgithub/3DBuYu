using Services;
using VContainer;
using VContainer.Unity;

/// <summary>
/// 注册 ProjectLifetimeScope 中跨场景共享的背包传输状态与物品目录。
/// 传输状态和目录均为根容器单例；商店场景组件仍由调用方注册。
/// </summary>
public sealed class ProjectInventoryInstaller : IInstaller
{
    private readonly ItemCatalog _itemCatalog;

    /// <summary>
    /// 创建物品注册器。
    /// </summary>
    /// <param name="itemCatalog">ProjectLifetimeScope 的物品目录；为空时使用默认空目录。</param>
    public ProjectInventoryInstaller(ItemCatalog itemCatalog)
    {
        _itemCatalog = itemCatalog;
    }

    /// <summary>
    /// 注册全局背包传输状态和配置优先的物品目录。
    /// </summary>
    /// <param name="builder">ProjectLifetimeScope 使用的容器构建器。</param>
    public void Install(IContainerBuilder builder)
    {
        builder.Register<InventoryTransferStorage>(Lifetime.Singleton)
            .As<IInventoryTransferStorage>()
            .AsSelf();
        builder.RegisterInstance(_itemCatalog != null ? _itemCatalog : ItemCatalog.Default).AsSelf();
    }
}
