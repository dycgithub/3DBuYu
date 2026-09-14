using _Project.UI.Common;
using UnityEngine;
using VContainer;
using VContainer.Unity;

/// <summary>
/// 保存 UIScene 的显式显示组件，并提供统一注册入口。
/// </summary>
public sealed class UiSceneComposition : MonoBehaviour
{
    [SerializeField] private PointsDisplayView _pointsDisplayView;

    /// <summary>
    /// 将非空 UIScene 显示组件注册到指定构建器。
    /// </summary>
    /// <param name="builder">UIScene 子容器构建器。</param>
    public void Register(IContainerBuilder builder)
    {
        if (_pointsDisplayView != null)
        {
            builder.RegisterComponent(_pointsDisplayView).AsSelf();
        }
    }
}
