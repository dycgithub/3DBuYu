using Supply;
using VContainer;
using VContainer.Unity;

/// <summary>持有随 Supply 场景离开而释放的开箱交互状态。</summary>
public class SupplySceneLifetimeScope : LifetimeScope
{
    /// <summary>注册场景级宝箱服务，重进场景时创建全新状态。</summary>
    protected override void Configure(IContainerBuilder builder)
    {
        builder.Register<ChestOpeningService>(Lifetime.Scoped).AsSelf();
    }
}
