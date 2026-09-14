using GameSystem;
using InventorySystem.Shop;
using Services;
using UnityEngine;
using VContainer;
using VContainer.Unity;

/// <summary>
/// 保存根容器所需的显式场景组件，并提供统一注册入口。
/// </summary>
public sealed class ProjectComposition : MonoBehaviour
{
    [SerializeField] private ResourceManager _resourceManager;
    [SerializeField] private AudioManager _audioManager;
    [SerializeField] private SceneLoader _sceneLoader;
    [SerializeField] private ShopManager _shopManager;

    /// <summary>
    /// 将非空根容器场景组件注册到指定构建器。
    /// </summary>
    /// <param name="builder">根容器构建器。</param>
    public void Register(IContainerBuilder builder)
    {
        if (_resourceManager != null)
        {
            builder.RegisterComponent(_resourceManager)
                .As<IPointsService>()
                .AsSelf();
        }

        if (_audioManager != null)
        {
            builder.RegisterComponent(_audioManager).AsSelf();
        }

        if (_sceneLoader != null)
        {
            builder.RegisterComponent(_sceneLoader).AsSelf();
        }

        if (_shopManager != null)
        {
            builder.RegisterComponent(_shopManager)
                .As<IShopService>()
                .AsSelf();
        }
    }
}
