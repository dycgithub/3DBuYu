using GameSystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Services;

/// <summary>
/// 全局根容器:改由 VContainerSettings.RootLifetimeScope 以 prefab 形式实例化并常驻(全工程唯一)。
/// 只注册"死不了"的服务:纯 C# 单例 + 随根容器常驻的场景组件(ResourceManager/ShopManager/SceneLoader)。
/// 场景组件请注册到 GameLoopLifetimeScope(子容器,每局重建)。
/// </summary>
public class ProjectLifetimeScope : LifetimeScope
{
    /// <summary>物品目录资产(可选):拖入后商店/存档按 id 查询使用;未配置时注册默认空目录。</summary>
    [SerializeField] private ItemCatalog itemCatalog;

    /// <summary>
    /// 初始化全局根容器并确保其生命周期跨场景保持。
    /// </summary>
    protected override void Awake()
    {
        // 根容器不应配置父引用;若有误配(如 parentReference 指向子作用域)则清除,避免 Build 失败
        if (parentReference.TypeName != null && parentReference.TypeName.Length > 0)
        {
            Debug.LogWarning($"[ProjectLifetimeScope] 根容器不应有父引用,已自动清除: {parentReference.TypeName}", this);
            parentReference = default;
        }

        base.Awake();
        if (Container == null)
        {
            Debug.LogWarning("[ProjectLifetimeScope] Container was null after base.Awake(), forcing Build()");
            Build();
        }

        // 根容器由 VContainerSettings 实例化时已 DontDestroyOnLoad,此处为冗余保险,无害。
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 注册跨场景服务、对象池和根场景组件。
    /// </summary>
    protected override void Configure(IContainerBuilder builder)
    {
        // === 单例服务(纯 C# 类,优先注册以便 [Inject] 可解析) ===
        // 对象池由根容器唯一持有,GameLoop/UIScene 子容器从父容器解析同一实例。
        new ProjectCoreInstaller().Install(builder);
        new ProjectCombatInstaller().Install(builder);
        builder.RegisterComponentInHierarchy<InventorySystem.Shop.ShopManager>().As<IShopService>().AsSelf();
        // === 物品目录(配置优先,默认空目录) ===
        new ProjectInventoryInstaller(itemCatalog).Install(builder);
        // 抽奖资源跨 UI 场景共享；开箱状态由 SupplySceneLifetimeScope 持有。
        new SupplyInstaller().Install(builder);
        // === DDOL 场景组件(挂在 ScopeContainer 下,永不销毁) ===
        builder.RegisterComponentInHierarchy<ResourceManager>().As<IPointsService>();
        builder.RegisterComponentInHierarchy<AudioManager>().AsSelf();
        InputSystem.InputService inputService =
            GetComponentInChildren<InputSystem.InputService>(true);
        if (inputService != null)
        {
            builder.RegisterComponent(inputService).As<IInputService>().AsSelf();
        }

        // === 启动场景组件 ===
        builder.RegisterComponentInHierarchy<SceneLoader>().AsSelf();
    }
}
