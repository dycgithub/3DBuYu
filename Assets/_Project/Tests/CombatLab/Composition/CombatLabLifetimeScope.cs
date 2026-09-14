using EffectSystem;
using FlockingSystem.ECS;
using Services;
using UnityEngine;
using VContainer;
using VContainer.Unity;

/// <summary>
/// CombatLab 的场景作用域，只承载显式组合和按模块选择的最小战斗服务。
/// </summary>
public sealed class CombatLabLifetimeScope : LifetimeScope
{
    [Header("Composition")]
    [SerializeField] private CombatLabComposition _composition;

    [Header("Effects")]
    [SerializeField] private CombatEffectCatalogSO _combatEffectCatalog;

    [Header("Flocking")]
    [SerializeField] private EnemyFlockSettingsSO _enemyFlockSettings;

    /// <summary>注册 CombatLab 组合、阶段服务和模块服务。</summary>
    /// <param name="builder">CombatLab 容器构建器。</param>
    protected override void Configure(IContainerBuilder builder)
    {
        if (_composition == null)
        {
            throw new System.InvalidOperationException(
                "CombatLabLifetimeScope 未配置 CombatLabComposition。");
        }

        _composition.Register(builder);
        builder.Register<CombatLabPhaseService>(Lifetime.Singleton)
            .As<ICombatPhaseService>()
            .AsSelf();
        new CombatLabInstaller(
            _composition,
            _combatEffectCatalog,
            _enemyFlockSettings,
            this).Install(builder);
    }
}
