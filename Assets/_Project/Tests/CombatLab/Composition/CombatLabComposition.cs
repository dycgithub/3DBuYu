using CameraSystem;
using EffectSystem;
using EnemySystem;
using EnemySystem.Spawning;
using InputSystem;
using Interfaces;
using Play;
using Services;
using SpatialSystem.Bridge;
using UnityEngine;
using VContainer;
using VContainer.Unity;

/// <summary>
/// 战斗测试场景的显式组合，区分必需组件、可选组件和测试模块。
/// </summary>
public sealed class CombatLabComposition : MonoBehaviour
{
    /// <summary>战斗测试场景可选择验证的模块。</summary>
    public enum CombatLabModuleType
    {
        Projectile,
        Damage,
        Ability,
        Buff,
        Enemy,
        Spatial,
        Effects
    }

    [Header("必需组件")]
    [SerializeField] private Camera _camera;
    [SerializeField] private CentralCore _centralCore;

    [Header("可选组件")]
    [SerializeField] private InputService _inputService;
    [SerializeField] private TransmitterFireController _transmitterFireController;
    [SerializeField] private PhysicsCentralDetector _physicsCentralDetector;
    [SerializeField] private PlayerController _playerController;
    [SerializeField] private PlayerCombatTarget _playerCombatTarget;
    [SerializeField] private SphereWalker _sphereWalker;
    [SerializeField] private StaminaController _staminaController;
    [SerializeField] private Enemy _enemy;
    [SerializeField] private EnemyPopulationController _enemyPopulationController;
    [SerializeField] private SpawnPositionProvider _spawnPositionProvider;
    [SerializeField] private SpatialRegistry _spatialRegistry;
    [SerializeField] private EffectManager _effectManager;
    [SerializeField] private PlayerCollisionFeedback _playerCollisionFeedback;
    [SerializeField] private SphericalCameraFollow _sphericalCameraFollow;
    [SerializeField] private SphericalCameraRegional _sphericalCameraRegional;
    [SerializeField] private SphericalCameraDirector _sphericalCameraDirector;
    [SerializeField] private CombatLabModuleType[] _modules;

    /// <summary>
    /// 将测试场景中已配置的组件注册到指定构建器；必需组件或 Projectile 来源缺失时拒绝构建。
    /// </summary>
    /// <param name="builder">战斗测试容器构建器。</param>
    public void Register(IContainerBuilder builder)
    {
        if (_camera == null || _centralCore == null)
        {
            throw new System.InvalidOperationException(
                "CombatLabComposition 必须配置 Camera 和 CentralCore。");
        }

        ValidateModuleSources();

        builder.RegisterComponent(_camera).AsSelf();
        builder.RegisterComponent(_centralCore).AsSelf();
        if (_inputService != null)
        {
            builder.RegisterComponent(_inputService)
                .As<IInputService>()
                .AsSelf();
        }

        RegisterOptional(builder, _transmitterFireController);
        RegisterOptional(builder, _physicsCentralDetector);
        RegisterOptional(builder, _playerController);
        if (_playerCombatTarget != null)
        {
            builder.RegisterComponent(_playerCombatTarget)
                .As<IPlayerCombatTarget>()
                .AsSelf();
        }

        RegisterOptional(builder, _sphereWalker);
        RegisterOptional(builder, _staminaController);
        RegisterOptional(builder, _enemy);
        if (_enemyPopulationController != null)
        {
            builder.RegisterComponent(_enemyPopulationController)
                .As<IEnemyPopulationService>()
                .AsSelf();
        }

        if (_spawnPositionProvider != null)
        {
            builder.RegisterComponent(_spawnPositionProvider)
                .As<ISpawnPositionProvider>()
                .AsSelf();
        }

        if (_spatialRegistry != null)
        {
            builder.RegisterComponent(_spatialRegistry)
                .As<ISpatialQueryService>()
                .AsSelf();
        }

        if (_effectManager != null)
        {
            builder.RegisterComponent(_effectManager)
                .As<IEffectService>()
                .As<IPooledEffectService>()
                .AsSelf();
        }

        RegisterOptional(builder, _playerCollisionFeedback);
        RegisterOptional(builder, _sphericalCameraFollow);
        RegisterOptional(builder, _sphericalCameraRegional);
        RegisterOptional(builder, _sphericalCameraDirector);
    }

    /// <summary>
    /// 校验模块所需的场景组件来源，避免容器构建时才报告接口缺失。
    /// </summary>
    private void ValidateModuleSources()
    {
        if (HasModule(CombatLabModuleType.Spatial) && _spatialRegistry == null)
        {
            throw new System.InvalidOperationException(
                "CombatLabComposition 的 Spatial 模块需要配置 SpatialRegistry，作为 ISpatialQueryService 来源。"
            );
        }

        if (HasModule(CombatLabModuleType.Effects) && _effectManager == null)
        {
            throw new System.InvalidOperationException(
                "CombatLabComposition 的 Effects 模块需要配置 EffectManager，作为 IEffectService 来源。"
            );
        }

        if (HasModule(CombatLabModuleType.Enemy) && _spawnPositionProvider == null)
        {
            throw new System.InvalidOperationException(
                "CombatLabComposition 的 Enemy 模块需要配置 SpawnPositionProvider，作为 ISpawnPositionProvider 来源。"
            );
        }

        if (!HasModule(CombatLabModuleType.Projectile))
        {
            return;
        }

        if (_spatialRegistry == null)
        {
            throw new System.InvalidOperationException(
                "CombatLabComposition 的 Projectile 模块需要配置 SpatialRegistry，作为 ISpatialQueryService 来源。"
            );
        }

        if (_effectManager == null)
        {
            throw new System.InvalidOperationException(
                "CombatLabComposition 的 Projectile 模块需要配置 EffectManager，作为 IEffectService 来源。"
            );
        }
    }

    private static void RegisterOptional<T>(IContainerBuilder builder, T component)
        where T : Component
    {
        if (component != null)
        {
            builder.RegisterComponent(component).AsSelf();
        }
    }

    /// <summary>
    /// 查询 CombatLab 是否启用了指定模块。
    /// </summary>
    /// <param name="module">要查询的 CombatLab 模块。</param>
    /// <returns>模块存在于当前组合时返回 true。</returns>
    public bool HasModule(CombatLabModuleType module)
    {
        if (_modules == null)
        {
            return false;
        }

        for (int index = 0; index < _modules.Length; index++)
        {
            if (_modules[index] == module)
            {
                return true;
            }
        }

        return false;
    }
}
