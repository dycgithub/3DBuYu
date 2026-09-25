using _Project.UI.Common;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using _Project.UI.Tutorial.Implement;

/// <summary>
/// UIScene(基地场景)的场景子容器,随 UIScene 卸载销毁、重新加载重建(与 GameLoopLifetimeScope 对称)。
/// 父容器为 ProjectLifetimeScope(全局根容器)。
/// 本容器职责:注册基地场景范围内的 PointsDisplayView 和 TutorialPanelController,并通过 autoInjectGameObjects
/// 把全局单例注入到 GridView / GridInteract 等多实例 UI 组件。
/// </summary>
public class UISceneLifetimeScope : LifetimeScope
{
    [Header("Item Tooltip")]
    [SerializeField] private bool hoverTooltipEnabled = false;
    [SerializeField] private bool selectedTooltipEnabled = true;

    /// <summary>
    /// 注册基地场景范围内的背包和提示交互服务。
    /// </summary>
    protected override void Configure(IContainerBuilder builder)
    {
        new UISceneInventoryInstaller(new ItemTooltipConfiguration
        {
            HoverEnabled = hoverTooltipEnabled,
            SelectedEnabled = selectedTooltipEnabled,
        }).Install(builder);
        builder.RegisterComponentInHierarchy<PointsDisplayView>().AsSelf();
        builder.RegisterComponentInHierarchy<TutorialPanelController>().AsSelf();
    }

}
