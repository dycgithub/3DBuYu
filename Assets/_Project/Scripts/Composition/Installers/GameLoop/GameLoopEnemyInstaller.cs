using System;
using EnemySystem.Spawning;
using FlockingSystem.ECS;
using Services;
using VContainer;
using VContainer.Unity;

/// <summary>
/// GameLoop 敌人段：保留原 Scope 提前注册的群游配置前置项，并注册后置的敌人生成服务。
/// 配置缺失时保持原 Scope 的启动异常；其余服务均为子容器单例。
/// </summary>
public sealed class GameLoopEnemyInstaller : IInstaller
{
    private readonly EnemyFlockSettingsSO _enemyFlockSettings;
    private readonly string _scopeName;

    /// <summary>
    /// 创建敌人注册器，并接收 Scope 名称以保持配置错误信息。
    /// </summary>
    /// <param name="enemyFlockSettings">GameLoopLifetimeScope 的群游配置资产。</param>
    /// <param name="scopeName">GameLoopLifetimeScope.name，用于配置错误信息。</param>
    public GameLoopEnemyInstaller(EnemyFlockSettingsSO enemyFlockSettings, string scopeName)
    {
        _enemyFlockSettings = enemyFlockSettings;
        _scopeName = scopeName;
    }

    /// <summary>
    /// 注册群游配置、群游运行服务、敌人池、工厂和生成入口。
    /// </summary>
    /// <param name="builder">GameLoopLifetimeScope 使用的容器构建器。</param>
    public void Install(IContainerBuilder builder)
    {
        if (_enemyFlockSettings == null)
        {
            throw new InvalidOperationException(
                $"[GameLoopLifetimeScope] {_scopeName}: 未配置 EnemyFlockSettings，无法启动 ECS Flocking。");
        }

        builder.RegisterInstance(_enemyFlockSettings).AsSelf();
        builder.Register<EnemyFlockRuntimeService>(Lifetime.Singleton).AsSelf();
        builder.Register<EnemyPool>(Lifetime.Singleton).AsSelf();
        builder.Register<EnemyFactory>(Lifetime.Singleton).AsSelf();
        builder.RegisterEntryPoint<EnemySpawner>(Lifetime.Singleton)
            .As<IEnemySpawner>()
            .As<IEnemyChildSpawner>()
            .AsSelf();
    }
}
