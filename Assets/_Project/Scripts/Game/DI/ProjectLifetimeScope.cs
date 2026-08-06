using GameSystem;
using InventorySystem.Shop;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using ShootingSystem;
using ShootingSystem.Bullets;
using ShootingSystem.Bullets.Effects;
using Services;

/// <summary>
/// 全局根容器,所在场景(GameUIScene)理论永不卸载,且本体 DontDestroyOnLoad 双保险。
/// 只注册"死不了"的服务:纯 C# 单例 + DDOL 挂载的场景组件(ResourceManager/ShopManager/SceneLoader)。
/// 场景组件请注册到 GameLoopLifetimeScope(子容器,每局重建)。
/// </summary>
public class ProjectLifetimeScope : LifetimeScope
{
    [Header("全局配置")]
    [SerializeField] private TurretSystem.TurretBase turretBase;

    [Header("商店经济配置(售价由商店负责)")]
    [SerializeField] private InventorySystem.Shop.ShopConfig shopConfig;

    [Header("物品表现配置(图标/UI 预制体由 UI 负责)")]
    [SerializeField] private ItemSystem.ItemVisualConfig[] itemVisualConfigs;

    /// <summary>
    /// 全局根容器单例（DDOL）。未注册到 DI 的场景组件解析全局服务时使用。
    /// </summary>
    public static ProjectLifetimeScope Instance { get; private set; }

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

        Instance = this;

        // 启动场景(GameUIScene)理论上永不卸载;DDOL 为保险,防止未来场景重构时容器意外销毁
        DontDestroyOnLoad(gameObject);
    }

    private new void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    protected override void Configure(IContainerBuilder builder)
    {
        // === 单例服务(纯 C# 类,优先注册以便 [Inject] 可解析) ===
        builder.Register<ItemSystem.ItemConfigRegistry>(Lifetime.Singleton).AsSelf();
        builder.Register<InventorySystem.PlacementService>(Lifetime.Singleton).As<Interfaces.IPlacementService>().AsSelf();
        builder.Register<_Project.UI.Common.UINotificationService>(Lifetime.Singleton).As<Services.IUINotificationService>().AsSelf();
        builder.Register<TimeManager>(Lifetime.Singleton).AsSelf();
        builder.Register<KillTimeRewardSource>(Lifetime.Singleton).As<ITimeRewardSource>().AsSelf();
        builder.Register<ProjectileVisualPool>(Lifetime.Singleton).AsSelf();
        builder.Register<EcsBulletSpawner>(Lifetime.Singleton).As<IBulletSpawner>().AsSelf();
        builder.Register<BulletEventBus>(Lifetime.Singleton).As<IBulletEventBus>().AsSelf();
        builder.Register<BulletEffectOrchestrator>(Lifetime.Singleton).AsSelf();
        builder.Register<TrajectoryPredictor>(Lifetime.Singleton).As<ITrajectorySimulationService>().AsSelf();

        // === 全局数据层(玩家仓库/装备配置,跨场景跨局) ===
        builder.RegisterInstance(turretBase);
        builder.Register<PlayerStorage>(Lifetime.Singleton).AsSelf();
        builder.Register<TurretSystem.PlayerLoadout>(Lifetime.Singleton).AsSelf();

        // === 商店经济配置(售价查询/购买/结算共用) ===
        builder.RegisterInstance(shopConfig);

        // === 物品表现注册表(icon/uiPrefab,UI 层消费) ===
        builder.RegisterInstance(new ItemSystem.ItemVisualRegistry(itemVisualConfigs));

        // === DDOL 场景组件(挂在 ScopeContainer 下,永不销毁) ===
        builder.RegisterComponentInHierarchy<ResourceManager>().As<IPointsService>().AsSelf();
        builder.RegisterComponentInHierarchy<ShopManager>().AsSelf();

        // === 启动场景组件 ===
        builder.RegisterComponentInHierarchy<SceneLoader>().AsSelf();
    }
}
