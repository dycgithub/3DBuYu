using System;
using Cinemachine;
using CombatSystem;
using GameSystem;
using Play;
using R3;
using Services;
using UnityEngine;
using VContainer;
using EnemySystem;

namespace CameraSystem
{
    /// <summary>
    /// 管理球面战斗的 Follow/Regional 模式，并调度共用镜头表现。
    /// <para>本类统一持有共用参数，并分别管理两个模式的初始化、可用状态和优先级。</para>
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class SphericalCameraDirector : MonoBehaviour
    {
        private const float MinimumFieldOfView = 1f;
        private const float MaximumFieldOfView = 179f;
        private const float DefaultFieldOfView = 61f;
        private const float MaximumCoverageRadius = 89f;
        private const float MinimumDelaySeconds = 0.001f;
        private const float DefaultFollowDelaySeconds = 0.2f;
        private const float DefaultReturnDelaySeconds = 0.1f;

        /// <summary>当前使用的摄像机模式。</summary>
        public enum CameraMode
        {
            Follow,
            Regional
        }

        [Header("模式")]
        [SerializeField, Tooltip("场景启动时使用的摄像机模式。")]
        private CameraMode _initialMode = CameraMode.Follow;
        [SerializeField, Tooltip("Follow 模式摄像机组件；留空时自动从子物体查找。")]
        private SphericalCameraFollow _follow;
        [SerializeField, Tooltip("Regional 模式摄像机组件；留空时自动从子物体查找。")]
        private SphericalCameraRegional _regional;
        [SerializeField, Range(0, 100), Tooltip("当前激活摄像机的 Cinemachine 优先级。")]
        private int _activePriority = 20;
        [SerializeField, Range(0, 100), Tooltip("未激活摄像机的 Cinemachine 优先级。")]
        private int _inactivePriority;

        [Header("摄像机引用")]
        [SerializeField, Tooltip("用于计算球面位置的角色移动组件；留空时使用 CentralCore 中的引用。")]
        private SphereWalker _walker;
        [SerializeField, Tooltip("Regional 模式使用的区域摄像机列表；留空时由 Regional 组件自动查找。")]
        private CinemachineVirtualCamera[] _regionCameras;

        [Header("镜头")]
        [SerializeField, Range(MinimumFieldOfView, MaximumFieldOfView), Tooltip("摄像机的基础垂直视野角，单位为度。")]
        private float _baseFieldOfView = DefaultFieldOfView;
        [SerializeField, Tooltip("用于播放镜头冲击效果的 Cinemachine 冲击源。")]
        private CinemachineImpulseSource _impulseSource;

        [Header("Follow 参数")]
        [SerializeField, Min(0f), Tooltip("摄像机相对球面半径的额外距离。")]
        private float _cameraDistanceOffset = 12.5f;
        [SerializeField, Min(MinimumDelaySeconds), Tooltip("玩家移动时摄像机追随方向的平滑时间，单位为秒。")]
        private float _followDelaySeconds = DefaultFollowDelaySeconds;
        [SerializeField, Min(MinimumDelaySeconds), Tooltip("玩家停止移动时摄像机回归方向的平滑时间，单位为秒。")]
        private float _returnDelaySeconds = DefaultReturnDelaySeconds;
        [SerializeField, Tooltip("沿球面切线向上的注视点偏移量。")]
        private float _lookAtVerticalOffset;

        [Header("Regional 参数")]
        [SerializeField, Min(0f), Tooltip("切换区域摄像机所需的额外角度差，单位为度，用于避免边界抖动。")]
        private float _switchHysteresisDegrees = 4f;
        [SerializeField, Range(0f, MaximumCoverageRadius), Tooltip("区域摄像机的目标覆盖半径，单位为度；设为 0 时自动计算。")]
        private float _coverageRadiusDegrees;

        [Header("敌群压力")]
        [SerializeField, Min(1), Tooltip("达到最大敌群压力所需的存活敌人数量。")]
        private int _pressureEnemyCount = 12;
        [SerializeField, Min(0f), Tooltip("敌群压力达到最大时增加的垂直视野角，单位为度。")]
        private float _pressureFovIncrease = 8f;
        [SerializeField, Min(0f), Tooltip("敌群压力权重的变化速度。")]
        private float _pressureBlendSpeed = 3f;

        [Header("敌群提示")]
        [SerializeField, Min(0f), Tooltip("敌群生成时镜头提示效果的持续时间，单位为秒。")]
        private float _populationIntroDuration = 1.2f;
        [SerializeField, Min(0f), Tooltip("敌群生成提示期间增加的垂直视野角，单位为度。")]
        private float _populationIntroFovIncrease = 6f;
        [SerializeField, Min(0f), Tooltip("敌群生成提示使用的镜头冲击强度。")]
        private float _populationImpulseForce = 0.12f;

        [Header("连杀冲击")]
        [SerializeField, Min(1), Tooltip("触发连杀镜头冲击所需的最低连杀数。")]
        private int _streakImpulseThreshold = 3;
        [SerializeField, Min(1), Tooltip("连杀冲击强度达到上限时对应的连杀数。")]
        private int _streakImpulseCap = 10;
        [SerializeField, Min(0f), Tooltip("达到触发连杀数时的最小镜头冲击强度。")]
        private float _streakMinImpulseForce = 0.08f;
        [SerializeField, Min(0f), Tooltip("达到连杀上限时的最大镜头冲击强度。")]
        private float _streakMaxImpulseForce = 0.22f;

        [Header("伤害冲击")]
        [SerializeField, Min(0f), Tooltip("普通受伤时的镜头冲击强度。")]
        private float _damageImpulseForce = 0.08f;
        [SerializeField, Min(0f), Tooltip("敌人被击杀时的镜头冲击强度。")]
        private float _deathImpulseForce = 0.16f;
        [SerializeField, Min(0f), Tooltip("两次镜头冲击之间的最短间隔，单位为秒。")]
        private float _impulseCooldown = 0.08f;

        [Inject] private IEnemyPopulationService _populationService;
        [Inject] private IKillStreakService _killStreak;
        [Inject] private IDamageEventSource _damageEventSource;
        [Inject] private GameManager _gameManager;
        [Inject] private CentralCore _centralCore;
        [Inject] private SphereWalker _injectedWalker;

        private CameraMode _currentMode;
        private float _pressureWeight;
        private float _populationIntroTimer;
        private float _lastImpulseTime = float.NegativeInfinity;
        private int _enemiesAlive;
        private int _lastStreak;
        private bool _runSettled;
        private bool _initialized;
        private bool _followReady;
        private bool _regionalReady;
        private bool _eventsSubscribed;

        private IDisposable _populationStartedSubscription;
        private IDisposable _enemyCountSubscription;
        private IDisposable _streakSubscription;

        /// <summary>获取当前摄像机模式。</summary>
        public CameraMode CurrentMode => _currentMode;

        /// <summary>获取当前模式使用的 Virtual Camera。</summary>
        public CinemachineVirtualCamera ActiveCamera
            => _currentMode == CameraMode.Follow
                ? _followReady ? _follow?.Camera : null
                : _regionalReady ? _regional?.ActiveCamera : null;

        private void Awake()
        {
            NormalizeConfiguration();
            ResolveReferences();
        }

        private void Start()
        {
            InitializeModes();
            SubscribeEvents();
            SetInitialMode();
        }

        private void OnEnable()
        {
            if (_initialized)
            {
                SubscribeEvents();
            }
        }

        private void LateUpdate()
        {
            if (!_initialized)
                return;

            float deltaTime = Time.deltaTime;
            if (!IsFinite(deltaTime) || deltaTime <= 0f)
                return;

            bool isPaused = _gameManager != null && _gameManager.CurrentState == GameState.Paused;
            if (!isPaused)
            {
                bool isPlaying = IsRunPlaying();
                if (isPlaying)
                    _populationIntroTimer = Mathf.Max(0f, _populationIntroTimer - deltaTime);
                else
                    _populationIntroTimer = 0f;

                bool shouldRestore = _runSettled || (_gameManager != null && !isPlaying);
                float pressureTarget = shouldRestore
                    ? 0f
                    : Mathf.Clamp01(_enemiesAlive / (float)Mathf.Max(1, _pressureEnemyCount));
                _pressureWeight = Mathf.MoveTowards(
                    _pressureWeight,
                    pressureTarget,
                    Mathf.Max(0f, _pressureBlendSpeed) * deltaTime);

                ApplyFieldOfView();
                if (_followReady)
                {
                    _follow?.Tick(
                        deltaTime,
                        _cameraDistanceOffset,
                        _followDelaySeconds,
                        _returnDelaySeconds,
                        _lookAtVerticalOffset);
                }
            }

            // 即使当前使用 Follow，也保持区域摄像机位置最新，切换时不会使用旧坐标。
            if (_regionalReady)
            {
                _regional?.Tick(
                    deltaTime,
                    _baseFieldOfView,
                    _switchHysteresisDegrees,
                    _coverageRadiusDegrees);
            }

            if (_currentMode == CameraMode.Regional && _regionalReady)
            {
                ApplyCameraPriorities();
            }
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        /// <summary>切换当前摄像机模式；Priority 只在这里统一管理。</summary>
        public void SetMode(CameraMode mode)
        {
            if (!_initialized)
            {
                InitializeModes();
            }

            if (!IsModeReady(mode))
            {
                Debug.LogWarning($"[SphericalCameraDirector] {mode} 摄像机不可用，保持当前模式。", this);
                return;
            }

            _currentMode = mode;
            if (_followReady)
            {
                _follow?.SetActive(mode == CameraMode.Follow);
            }

            if (_regionalReady)
            {
                _regional?.SetActive(mode == CameraMode.Regional);
            }

            if (mode == CameraMode.Regional && _regionalReady)
            {
                _regional?.Tick(
                    0f,
                    _baseFieldOfView,
                    _switchHysteresisDegrees,
                    _coverageRadiusDegrees);
            }

            ApplyCameraPriorities();
            ApplyFieldOfView();
        }

        /// <summary>手动切换到 Follow 模式。</summary>
        public void UseFollowCamera() => SetMode(CameraMode.Follow);

        /// <summary>手动切换到 Regional 模式。</summary>
        public void UseRegionalCamera() => SetMode(CameraMode.Regional);

        private void InitializeModes()
        {
            ResolveReferences();

            SphereWalker walker = _walker != null
                ? _walker
                : _injectedWalker != null
                    ? _injectedWalker
                    : _centralCore != null ? _centralCore.SphereWalker : null;

            _followReady = InitializeFollow(walker);
            _regionalReady = InitializeRegional(walker);
            _initialized = _followReady || _regionalReady;
        }

        private bool InitializeFollow(SphereWalker walker)
        {
            if (_follow == null || walker == null)
            {
                return false;
            }

            _follow.Initialize(walker);
            return _follow.Camera != null;
        }

        private bool InitializeRegional(SphereWalker walker)
        {
            if (_regional == null || walker == null)
            {
                return false;
            }

            if (_regionCameras != null && _regionCameras.Length > 0)
            {
                _regional.Initialize(walker, _regionCameras);
            }
            else
            {
                _regional.Initialize(walker);
            }

            return _regional.RegionCameras.Length > 0;
        }

        private void ResolveReferences()
        {
            if (_follow == null)
                _follow = GetComponentInChildren<SphericalCameraFollow>(true);
            if (_regional == null)
                _regional = GetComponentInChildren<SphericalCameraRegional>(true);
            if (_impulseSource == null)
                _impulseSource = GetComponent<CinemachineImpulseSource>();
        }

        private void SetInitialMode()
        {
            if (IsModeReady(_initialMode))
            {
                SetMode(_initialMode);
                return;
            }

            CameraMode fallback = _initialMode == CameraMode.Follow
                ? CameraMode.Regional
                : CameraMode.Follow;
            if (IsModeReady(fallback))
            {
                Debug.LogWarning(
                    $"[SphericalCameraDirector] 初始模式 {_initialMode} 不可用，回退到 {fallback}。",
                    this);
                SetMode(fallback);
                return;
            }

            Debug.LogError("[SphericalCameraDirector] Follow 和 Regional 摄像机均不可用。", this);
        }

        private bool IsModeReady(CameraMode mode)
        {
            return mode == CameraMode.Follow ? _followReady : _regionalReady;
        }

        private void ApplyCameraPriorities()
        {
            if (_followReady && _follow?.Camera != null)
            {
                _follow.Camera.Priority = _currentMode == CameraMode.Follow
                    ? _activePriority
                    : _inactivePriority;
            }

            if (_regionalReady)
            {
                _regional?.SetPriority(_inactivePriority);
            }

            if (_currentMode == CameraMode.Regional &&
                _regionalReady &&
                _regional?.ActiveCamera != null)
            {
                _regional.ActiveCamera.Priority = _activePriority;
            }
        }

        private void ApplyFieldOfView()
        {
            float populationWeight = GetPopulationIntroWeight();
            float targetFieldOfView = Mathf.Clamp(
                _baseFieldOfView +
                _pressureWeight * Mathf.Max(0f, _pressureFovIncrease) +
                populationWeight * Mathf.Max(0f, _populationIntroFovIncrease),
                MinimumFieldOfView,
                MaximumFieldOfView);

            if (_followReady)
            {
                _follow?.SetFieldOfView(targetFieldOfView);
            }

            if (_regionalReady)
            {
                _regional?.SetFieldOfView(targetFieldOfView);
            }
        }

        private void SubscribeEvents()
        {
            if (_eventsSubscribed)
                return;

            SyncRunState();
            if (_populationService != null)
            {
                _enemiesAlive = Mathf.Max(0, _populationService.EnemiesAlive);
                // 数量调度只在开局播放入场反馈，补怪不重复触发旧的周期震动。
                _populationStartedSubscription = _populationService.OnPopulationStarted.Subscribe(_ => HandlePopulationStarted(0));
                _enemyCountSubscription = _populationService.OnEnemiesAliveChanged.Subscribe(HandleEnemyCountChanged);
            }

            if (_killStreak != null)
                _streakSubscription = _killStreak.CurrentStreak.Subscribe(HandleStreakChanged);

            if (_damageEventSource != null)
            {
                _damageEventSource.DamageApplied += HandleDamageApplied;
            }

            if (_gameManager != null)
            {
                _gameManager.OnGameStarted += HandleRunStarted;
                _gameManager.OnSettled += HandleRunSettled;
            }

            _eventsSubscribed = true;
        }

        private void UnsubscribeEvents()
        {
            _populationStartedSubscription?.Dispose();
            _populationStartedSubscription = null;
            _enemyCountSubscription?.Dispose();
            _enemyCountSubscription = null;
            _streakSubscription?.Dispose();
            _streakSubscription = null;

            if (_damageEventSource != null)
            {
                _damageEventSource.DamageApplied -= HandleDamageApplied;
            }

            if (_eventsSubscribed && _gameManager != null)
            {
                _gameManager.OnGameStarted -= HandleRunStarted;
                _gameManager.OnSettled -= HandleRunSettled;
            }

            _eventsSubscribed = false;
        }

        private void HandlePopulationStarted(int _)
        {
            if (_runSettled || !IsRunPlaying())
                return;

            _populationIntroTimer = Mathf.Max(_populationIntroTimer, Mathf.Max(0f, _populationIntroDuration));
            GenerateImpulse(_populationImpulseForce);
        }

        private void HandleEnemyCountChanged(int enemiesAlive)
        {
            _enemiesAlive = Mathf.Max(0, enemiesAlive);
        }

        private void HandleStreakChanged(int streak)
        {
            int normalizedStreak = Mathf.Max(0, streak);
            if (!_runSettled && IsRunPlaying() &&
                normalizedStreak > _lastStreak && normalizedStreak >= _streakImpulseThreshold)
            {
                int cap = Mathf.Max(_streakImpulseThreshold, _streakImpulseCap);
                float progress = Mathf.InverseLerp(_streakImpulseThreshold, cap, normalizedStreak);
                float force = Mathf.Lerp(
                    Mathf.Max(0f, _streakMinImpulseForce),
                    Mathf.Max(0f, _streakMaxImpulseForce),
                    progress);
                GenerateImpulse(force);
            }

            _lastStreak = normalizedStreak;
        }

        private void HandleDamageApplied(DamageEvent damageEvent)
        {
            if (damageEvent.Target is Enemy && !_runSettled && IsRunPlaying())
            {
                GenerateImpulse(
                    damageEvent.WasKilled ? _deathImpulseForce : _damageImpulseForce);
            }
        }

        private void HandleRunStarted()
        {
            _runSettled = false;
            _pressureWeight = 0f;
            _populationIntroTimer = 0f;
            _lastStreak = 0;
            _lastImpulseTime = float.NegativeInfinity;
            _enemiesAlive = _populationService != null ? Mathf.Max(0, _populationService.EnemiesAlive) : 0;
            SetInitialMode();
        }

        private void HandleRunSettled(bool _, int __)
        {
            _runSettled = true;
            _populationIntroTimer = 0f;
        }

        private float GetPopulationIntroWeight()
        {
            float duration = Mathf.Max(0f, _populationIntroDuration);
            if (duration <= 0f)
                return 0f;

            float normalized = Mathf.Clamp01(_populationIntroTimer / duration);
            return normalized * normalized * (3f - 2f * normalized);
        }

        private void GenerateImpulse(float force)
        {
            if (_impulseSource == null || !_impulseSource.isActiveAndEnabled ||
                !IsFinite(force) || force <= 0f)
                return;

            float currentTime = Time.unscaledTime;
            if (_impulseCooldown > 0f && currentTime - _lastImpulseTime < _impulseCooldown)
                return;

            _lastImpulseTime = currentTime;
            Vector3 origin = _centralCore != null && _centralCore.SphereWalker != null
                ? _centralCore.SphereWalker.transform.position
                : transform.position;
            _impulseSource.GenerateImpulseAtPositionWithVelocity(origin, Vector3.down * force);
        }

        private bool IsRunPlaying()
        {
            return _gameManager == null || _gameManager.CurrentState == GameState.Playing;
        }

        private void SyncRunState()
        {
            if (_populationService != null)
                _enemiesAlive = Mathf.Max(0, _populationService.EnemiesAlive);
            if (_gameManager == null)
                return;

            GameState state = _gameManager.CurrentState;
            _runSettled = state is GameState.Menu or GameState.Settled or GameState.Supply or GameState.Failed;
        }

        private void OnValidate()
        {
            NormalizeConfiguration();
        }

        private void NormalizeConfiguration()
        {
            _activePriority = Mathf.Clamp(_activePriority, 0, 100);
            _inactivePriority = Mathf.Clamp(_inactivePriority, 0, _activePriority);
            _baseFieldOfView = NormalizeFieldOfView(_baseFieldOfView);
            _cameraDistanceOffset = NormalizeNonNegative(_cameraDistanceOffset);
            _followDelaySeconds = NormalizePositive(_followDelaySeconds, DefaultFollowDelaySeconds);
            _returnDelaySeconds = NormalizePositive(_returnDelaySeconds, DefaultReturnDelaySeconds);
            _lookAtVerticalOffset = IsFinite(_lookAtVerticalOffset) ? _lookAtVerticalOffset : 0f;
            _switchHysteresisDegrees = NormalizeNonNegative(_switchHysteresisDegrees);
            _coverageRadiusDegrees = Mathf.Min(
                MaximumCoverageRadius,
                NormalizeNonNegative(_coverageRadiusDegrees));
            _pressureEnemyCount = Mathf.Max(1, _pressureEnemyCount);
            _pressureFovIncrease = NormalizeNonNegative(_pressureFovIncrease);
            _pressureBlendSpeed = NormalizeNonNegative(_pressureBlendSpeed);
            _populationIntroDuration = NormalizeNonNegative(_populationIntroDuration);
            _populationIntroFovIncrease = NormalizeNonNegative(_populationIntroFovIncrease);
            _populationImpulseForce = NormalizeNonNegative(_populationImpulseForce);
            _streakImpulseThreshold = Mathf.Max(1, _streakImpulseThreshold);
            _streakImpulseCap = Mathf.Max(_streakImpulseThreshold, _streakImpulseCap);
            _streakMinImpulseForce = NormalizeNonNegative(_streakMinImpulseForce);
            _streakMaxImpulseForce = Mathf.Max(
                _streakMinImpulseForce,
                NormalizeNonNegative(_streakMaxImpulseForce));
            _damageImpulseForce = NormalizeNonNegative(_damageImpulseForce);
            _deathImpulseForce = NormalizeNonNegative(_deathImpulseForce);
            _impulseCooldown = NormalizeNonNegative(_impulseCooldown);
        }

        private static float NormalizeFieldOfView(float value)
        {
            return IsFinite(value)
                ? Mathf.Clamp(value, MinimumFieldOfView, MaximumFieldOfView)
                : DefaultFieldOfView;
        }

        private static float NormalizeNonNegative(float value)
        {
            return IsFinite(value) ? Mathf.Max(0f, value) : 0f;
        }

        private static float NormalizePositive(float value, float fallback)
        {
            return IsFinite(value) ? Mathf.Max(MinimumDelaySeconds, value) : fallback;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
