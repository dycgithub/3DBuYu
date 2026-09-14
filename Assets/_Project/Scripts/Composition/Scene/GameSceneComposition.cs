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
using _Project.UI.Card;
using _Project.UI.Common;
using _Project.UI.Settings;
using CombatSystem;

/// <summary>
/// 保存 GameScene 的显式场景组件，并提供统一注册入口。
/// </summary>
public sealed class GameSceneComposition : MonoBehaviour
{
    [SerializeField] private InputService _inputService;
    [SerializeField] private EnemyPopulationController _enemyPopulationController;
    [SerializeField] private SpawnPositionProvider _spawnPositionProvider;
    [SerializeField] private SpatialRegistry _spatialRegistry;
    [SerializeField] private GameSystem.GameManager _gameManager;
    [SerializeField] private SphericalCameraFollow _sphericalCameraFollow;
    [SerializeField] private SphericalCameraRegional _sphericalCameraRegional;
    [SerializeField] private SphericalCameraDirector _sphericalCameraDirector;
    [SerializeField] private EffectManager _effectManager;
    [SerializeField] private Camera _camera;
    [SerializeField] private CentralCore _centralCore;
    [SerializeField] private TransmitterFireController _transmitterFireController;
    [SerializeField] private PhysicsCentralDetector _physicsCentralDetector;
    [SerializeField] private PlayerController _playerController;
    [SerializeField] private PlayerCombatTarget _playerCombatTarget;
    [SerializeField] private SphereWalker _sphereWalker;
    [SerializeField] private StaminaController _staminaController;
    [SerializeField] private PlayerCollisionFeedback _playerCollisionFeedback;
    [SerializeField] private CardHandView _cardHandView;
    [SerializeField] private TransmitterBackpackBinder _transmitterBackpackBinder;
    [SerializeField] private PointsFlyEffectPresenter _pointsFlyEffectPresenter;
    [SerializeField] private GameintStatisticsUIToolkit _gameintStatisticsUiToolkit;
    [SerializeField] private DifficultySelector _difficultySelector;

    /// <summary>
    /// 将非空 GameScene 组件按当前 Scope 的接口映射注册到指定构建器。
    /// </summary>
    /// <param name="builder">GameScene 子容器构建器。</param>
    public void Register(IContainerBuilder builder)
    {
        if (_inputService != null)
        {
            builder.RegisterComponent(_inputService)
                .As<IInputService>()
                .AsSelf();
        }

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

        if (_gameManager != null)
        {
            builder.RegisterComponent(_gameManager)
                .As<IGameEventService>()
                .As<ICombatPhaseService>()
                .AsSelf();
        }

        RegisterOptional(builder, _sphericalCameraFollow);
        RegisterOptional(builder, _sphericalCameraRegional);
        RegisterOptional(builder, _sphericalCameraDirector);

        if (_effectManager != null)
        {
            builder.RegisterComponent(_effectManager)
                .As<IEffectService>()
                .As<IPooledEffectService>()
                .AsSelf();
        }

        RegisterOptional(builder, _camera);
        RegisterOptional(builder, _centralCore);
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
        RegisterOptional(builder, _playerCollisionFeedback);
        RegisterOptional(builder, _cardHandView);
        RegisterOptional(builder, _transmitterBackpackBinder);
        RegisterOptional(builder, _pointsFlyEffectPresenter);
        RegisterOptional(builder, _gameintStatisticsUiToolkit);
        RegisterOptional(builder, _difficultySelector);
    }

    private static void RegisterOptional<T>(IContainerBuilder builder, T component)
        where T : Component
    {
        if (component != null)
        {
            builder.RegisterComponent(component).AsSelf();
        }
    }
}
