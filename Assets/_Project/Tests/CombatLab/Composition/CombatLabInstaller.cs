using CombatSystem;
using EffectSystem;
using FlockingSystem.ECS;
using GameSystem;
using Services;
using UnityEngine;
using VContainer;
using VContainer.Unity;

/// <summary>
/// 按 CombatLab 模块安装最小战斗服务，不引入完整游戏流程或库存绑定。
/// </summary>
public sealed class CombatLabInstaller : IInstaller
{
    private readonly CombatLabComposition _composition;
    private readonly CombatEffectCatalogSO _combatEffectCatalog;
    private readonly EnemyFlockSettingsSO _enemyFlockSettings;
    private readonly UnityEngine.Object _logContext;

    /// <summary>创建按 CombatLab 模块安装服务的注册器。</summary>
    /// <param name="composition">提供模块查询和场景组件映射的组合。</param>
    /// <param name="combatEffectCatalog">启用 Effects 时必需的战斗特效目录。</param>
    /// <param name="enemyFlockSettings">启用 Enemy 时必需的敌群配置。</param>
    /// <param name="logContext">配置错误日志上下文。</param>
    public CombatLabInstaller(
        CombatLabComposition composition,
        CombatEffectCatalogSO combatEffectCatalog,
        EnemyFlockSettingsSO enemyFlockSettings,
        UnityEngine.Object logContext)
    {
        _composition = composition;
        _combatEffectCatalog = combatEffectCatalog;
        _enemyFlockSettings = enemyFlockSettings;
        _logContext = logContext;
    }

    /// <summary>按模块安装 CombatLab 的最小依赖闭包。</summary>
    /// <param name="builder">CombatLab Scope 的容器构建器。</param>
    public void Install(IContainerBuilder builder)
    {
        bool projectile = HasModule(CombatLabComposition.CombatLabModuleType.Projectile);
        bool damage = HasModule(CombatLabComposition.CombatLabModuleType.Damage);
        bool ability = HasModule(CombatLabComposition.CombatLabModuleType.Ability);
        bool effects = HasModule(CombatLabComposition.CombatLabModuleType.Effects);

        if (projectile && !effects)
        {
            throw new System.InvalidOperationException(
                "CombatLab 的 Projectile 模块需要 Effects 模块提供 IEffectService。");
        }

        if (effects && _combatEffectCatalog == null)
        {
            throw new System.InvalidOperationException(
                "CombatLab 的 Effects 模块需要配置 CombatEffectCatalogSO，无法注册特效服务。"
            );
        }

        if (projectile || damage || ability)
        {
            new ProjectCombatInstaller().Install(builder);
        }

        if (projectile)
        {
            new ProjectCoreInstaller().Install(builder);
            builder.Register<EnergyService>(Lifetime.Singleton)
                .As<IEnergyService>()
                .AsSelf();
            new GameLoopProjectileInstaller().Install(builder);
            builder.Register<TransmitterAttackBuildService>(Lifetime.Singleton).AsSelf();
            builder.Register<TransmitterAttackService>(Lifetime.Singleton)
                .As<ITransmitterAttackService>()
                .AsSelf();
        }
        else if (damage || ability)
        {
            RegisterDamage(builder);
        }

        if (ability)
        {
            if (!projectile)
            {
                builder.Register<AttackInstanceIdGenerator>(Lifetime.Singleton)
                    .As<IAttackInstanceIdGenerator>()
                    .AsSelf();
            }

            builder.Register<CentralSkillEffectService>(Lifetime.Singleton)
                .As<ICentralSkillEffectService>()
                .As<ICentralSkillEffectResetService>()
                .AsSelf();
            builder.Register<SkillExecutor>(Lifetime.Singleton)
                .As<ISkillExecutionService>()
                .AsSelf();
        }

        if (effects)
        {
            new GameLoopEffectInstaller(_combatEffectCatalog, _logContext).Install(builder);
        }

        if (HasModule(CombatLabComposition.CombatLabModuleType.Enemy))
        {
            string scopeName = _logContext != null ? _logContext.name : "CombatLab";
            new GameLoopEnemyInstaller(_enemyFlockSettings, scopeName).Install(builder);
        }
    }

    private bool HasModule(CombatLabComposition.CombatLabModuleType module)
    {
        return _composition != null && _composition.HasModule(module);
    }

    private static void RegisterDamage(IContainerBuilder builder)
    {
        builder.Register<DamageRequestProcessor>(Lifetime.Singleton)
            .As<IDamageRequestProcessor>()
            .AsSelf();
    }
}
