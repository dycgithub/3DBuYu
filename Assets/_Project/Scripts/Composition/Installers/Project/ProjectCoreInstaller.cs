using Services;
using Utils;
using VContainer;
using VContainer.Unity;

/// <summary>
/// 注册 ProjectLifetimeScope 使用的全局核心服务。
/// 纯 C# 服务由根容器创建；依赖根作用域场景组件的服务通过同一根容器解析。
/// </summary>
public sealed class ProjectCoreInstaller : IInstaller
{
    /// <summary>
    /// 注册全局通知、对象池和设置服务。
    /// </summary>
    /// <param name="builder">ProjectLifetimeScope 使用的容器构建器。</param>
    public void Install(IContainerBuilder builder)
    {
        builder.Register<_Project.UI.Common.UINotificationService>(Lifetime.Singleton)
            .As<Services.IUINotificationService>()
            .AsSelf();
        builder.Register<GameObjectPoolService>(Lifetime.Singleton)
            .As<IGameObjectPool>()
            .AsSelf();
        builder.Register<OptionsService>(Lifetime.Singleton).AsSelf();
    }
}
