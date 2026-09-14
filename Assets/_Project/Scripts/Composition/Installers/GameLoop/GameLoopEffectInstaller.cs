using EffectSystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;

/// <summary>
/// GameLoop 特效段：保留原 Scope 提前注册的目录配置，并注册后置的升级表现服务。
/// 目录为空时保持原 Scope 的错误日志；升级服务为子容器单例。
/// </summary>
public sealed class GameLoopEffectInstaller : IInstaller
{
    private readonly CombatEffectCatalogSO _combatEffectCatalog;
    private readonly UnityEngine.Object _logContext;

    /// <summary>
    /// 创建特效注册器。
    /// </summary>
    /// <param name="combatEffectCatalog">GameLoopLifetimeScope 的战斗特效目录，可为空。</param>
    /// <param name="logContext">原 Scope 对应的日志上下文对象。</param>
    public GameLoopEffectInstaller(
        CombatEffectCatalogSO combatEffectCatalog,
        UnityEngine.Object logContext)
    {
        _combatEffectCatalog = combatEffectCatalog;
        _logContext = logContext;
    }

    /// <summary>
    /// 注册战斗特效目录和升级特效播放服务。
    /// </summary>
    /// <param name="builder">GameLoopLifetimeScope 使用的容器构建器。</param>
    public void Install(IContainerBuilder builder)
    {
        if (_combatEffectCatalog != null)
        {
            builder.RegisterInstance(_combatEffectCatalog).AsSelf();
        }
        else
        {
            Debug.LogError("[GameLoopLifetimeScope] 未配置 CombatEffectCatalogSO。", _logContext);
        }

        builder.Register<UpgradeEffectPlayer>(Lifetime.Singleton).AsSelf();
    }
}
