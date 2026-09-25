using _Project.UI.Common;
using Services;
using VContainer;
using VContainer.Unity;

/// <summary>
/// 注册 UIScene 子容器独有的背包状态与固定模式提示服务。
/// 不向 ProjectLifetimeScope 注册 InventoryManager 或 ItemTooltipManager。
/// </summary>
public sealed class UISceneInventoryInstaller : IInstaller
{
    private readonly ItemTooltipConfiguration _tooltipConfiguration;

    /// <summary>
    /// 创建 UIScene 背包注册器。
    /// </summary>
    /// <param name="tooltipConfiguration">Tooltip 显示开关。</param>
    public UISceneInventoryInstaller(ItemTooltipConfiguration tooltipConfiguration)
    {
        _tooltipConfiguration = tooltipConfiguration;
    }

    /// <summary>
    /// 注册当前 UIScene 的背包状态和固定模式提示服务。
    /// </summary>
    /// <param name="builder">UISceneLifetimeScope 使用的容器构建器。</param>
    public void Install(IContainerBuilder builder)
    {
        builder.RegisterEntryPoint<InventoryManager>(Lifetime.Singleton)
            .AsSelf();
        builder.Register<ItemTooltipManager>(Lifetime.Singleton)
            .As<IItemTooltipService>()
            .AsSelf()
            .WithParameter<ItemTooltipMode>(ItemTooltipMode.Fixed)
            .WithParameter("hoverEnabled", _tooltipConfiguration.HoverEnabled)
            .WithParameter("selectedEnabled", _tooltipConfiguration.SelectedEnabled);
    }
}
