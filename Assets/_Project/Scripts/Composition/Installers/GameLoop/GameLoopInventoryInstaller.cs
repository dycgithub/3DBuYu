using _Project.UI.Common;
using Services;
using VContainer;
using VContainer.Unity;

/// <summary>
/// GameLoop 前置注册段：先注册背包状态，再注册依赖它的移动提示服务。
/// 两者均为当前 GameLoop 子容器单例。
/// </summary>
public sealed class GameLoopInventoryInstaller : IInstaller
{
    private readonly ItemTooltipConfiguration _tooltipConfiguration;

    /// <summary>
    /// 创建 GameLoop 背包注册器。
    /// </summary>
    /// <param name="tooltipConfiguration">Tooltip 显示开关。</param>
    public GameLoopInventoryInstaller(ItemTooltipConfiguration tooltipConfiguration)
    {
        _tooltipConfiguration = tooltipConfiguration;
    }

    /// <summary>
    /// 注册当前 GameLoop 的背包状态和移动模式提示服务。
    /// </summary>
    /// <param name="builder">GameLoopLifetimeScope 使用的容器构建器。</param>
    public void Install(IContainerBuilder builder)
    {
        builder.RegisterEntryPoint<InventoryManager>(Lifetime.Singleton)
            .As<IInventoryDragState>()
            .As<IInventorySelectionState>()
            .AsSelf();
        builder.Register<ItemTooltipManager>(Lifetime.Singleton)
            .As<IItemTooltipService>()
            .AsSelf()
            .WithParameter<ItemTooltipMode>(ItemTooltipMode.Moving)
            .WithParameter("hoverEnabled", _tooltipConfiguration.HoverEnabled)
            .WithParameter("selectedEnabled", _tooltipConfiguration.SelectedEnabled);
    }
}
