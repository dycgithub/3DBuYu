using _Project.UI.Common;
using Services;
using UnityEngine;
using VContainer;
using VContainer.Unity;

/// <summary>
/// UIScene(基地场景)的场景子容器,随 UIScene 卸载销毁、重新加载重建(与 GameLoopLifetimeScope 对称)。
/// 父容器为 ProjectLifetimeScope(全局根容器)。
/// 本容器职责:通过 autoInjectGameObjects 把全局单例注入到 UIScene 的多实例 UI 组件
/// (GridView / GridInteract / PointsDisplayView),替代原先 ProjectLifetimeScope.Instance.Container 服务定位器写法。
/// 多实例组件无法用 RegisterComponentInHierarchy 逐个注册,统一走 autoInjectGameObjects(在编辑器里把相关物体拖入)。
/// </summary>
public class UISceneLifetimeScope : LifetimeScope
{
    [Header("Item Tooltip")]
    [SerializeField] private bool hoverTooltipEnabled = false;
    [SerializeField] private bool selectedTooltipEnabled = true;

    protected override void Configure(IContainerBuilder builder)
    {
        builder.RegisterEntryPoint<InventoryManager>(Lifetime.Singleton)
            .As<IInventoryDragState>()
            .As<IInventorySelectionState>()
            .AsSelf();
        builder.Register<ItemTooltipManager>(Lifetime.Singleton)
            .As<IItemTooltipService>()
            .AsSelf()
            .WithParameter<ItemTooltipMode>(ItemTooltipMode.Fixed)
            .WithParameter("hoverEnabled", hoverTooltipEnabled)
            .WithParameter("selectedEnabled", selectedTooltipEnabled);
    }

}
