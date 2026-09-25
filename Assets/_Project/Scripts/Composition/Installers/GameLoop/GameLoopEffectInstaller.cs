using EffectSystem;
using UnityEngine;
using VContainer;
using VContainer.Unity;

/// <summary>
/// GameLoop 特效段：注册当前场景使用的战斗特效目录。
/// 目录缺失时保留原有错误日志，但不注册空引用。
/// </summary>
public sealed class GameLoopEffectInstaller : IInstaller
{
    private readonly CombatEffectCatalogSO _combatEffectCatalog;
    private readonly UnityEngine.Object _logContext;

    /// <summary>
    /// 创建特效注册器。
    /// </summary>
    /// <param name="combatEffectCatalog">GameLoopLifetimeScope 的战斗特效目录，可为空。</param>
    /// <param name="logContext">GameLoopLifetimeScope 的日志上下文对象。</param>
    public GameLoopEffectInstaller(
        CombatEffectCatalogSO combatEffectCatalog,
        UnityEngine.Object logContext)
    {
        _combatEffectCatalog = combatEffectCatalog;
        _logContext = logContext;
    }

    /// <summary>
    /// 注册战斗特效目录。
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
    }
}
