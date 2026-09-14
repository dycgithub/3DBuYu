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
    /// <para>本类是摄像机模式的唯一所有者；具体摄像机的位置和区域计算分别由两个模式脚本负责，命中和击杀使用不同冲击强度。</para>
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class SphericalCameraDirector : MonoBehaviour
    {
        private const float MinimumFieldOfView = 1f;
        private const float MaximumFieldOfView = 179f;
        private const float DefaultFieldOfView = 61f;

        /// <summary>当前使用的摄像机模式。</summary>
        public enum CameraMode
        {
            Follow,
            Regional
        }

        [Header("模式")]
        [SerializeField] private CameraMode _initialMode = CameraMode.Follow;
        [SerializeField] private SphericalCameraFollow _follow;
        [SerializeField] private SphericalCameraRegional _regional;
        [SerializeField, Range(0, 100)] private int _activePriority = 20;
        [SerializeField, Range(0, 100)] private int _inactivePriority;

        [Header("镜头")]
        [SerializeField, Range(MinimumFieldOfView, MaximumFieldOfView)] private float _baseFieldOfView =
            DefaultFieldOfView;
        [SerializeField] private CinemachineImpulseSource _impulseSource;

        [Header("敌群压力")]
        [SerializeField, Min(1)] private int _pressureEnemyCount = 12;
        [SerializeField, Min(0f)] private float _pressureFovIncrease = 8f;
        [SerializeField, Min(0f)] private float _pressureBlendSpeed = 3f;

        [Header("敌群提示")]
        [SerializeField, Min(0f)] private float _populationIntroDuration = 1.2f;
        [SerializeField, Min(0f)] private float _populationIntroFovIncrease = 6f;
        [SerializeField, Min(0f)] private float _populationImpulseForce = 0.12f;

        [Header("连杀冲击")]
        [SerializeField, Min(1)] private int _streakImpulseThreshold = 3;
        [SerializeField, Min(1)] private int _streakImpulseCap = 10;
        [SerializeField, Min(0f)] private float _streakMinImpulseForce = 0.08f;
        [SerializeField, Min(0f)] private float _streakMaxImpulseForce = 0.22f;

        [Header("伤害冲击")]
        [SerializeField, Min(0f)] private float _damageImpulseForce = 0.08f;
        [SerializeField, Min(0f)] private float _deathImpulseForce = 0.16f;
        [SerializeField, Min(0f)] private float _impulseCooldown = 0.08f;

        [Inject] private IEnemyPopulationService _populationService;
        [Inject] private IKillStreakService _killStreak;
        [Inject] private GameManager _gameManager;
        [Inject] private CentralCore _centralCore;

        private CameraMode _currentMode;
        private float _pressureWeight;
        private float _populationIntroTimer;
        private float _lastImpulseTime = float.NegativeInfinity;
        private int _enemiesAlive;
        private int _lastStreak;
        private bool _runSettled;
        private bool _initialized;
        private bool _eventsSubscribed;

        private IDisposable _populationStartedSubscription;
        private IDisposable _enemyCountSubscription;
        private IDisposable _streakSubscription;

        /// <summary>获取当前摄像机模式。</summary>
        public CameraMode CurrentMode => _currentMode;

        /// <summary>获取当前模式使用的 Virtual Camera。</summary>
        public CinemachineVirtualCamera ActiveCamera
            => _currentMode == CameraMode.Follow ? _follow?.Camera : _regional?.ActiveCamera;

        private void Awake()
        {
            NormalizeConfiguration();
            ResolveReferences();
        }

        private void Start()
        {
            InitializeModes();
            SubscribeEvents();
            SetMode(_initialMode);
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
                _follow?.Tick(deltaTime);
            }

            // 即使当前使用 Follow，也保持区域摄像机位置最新，切换时不会使用旧坐标。
            _regional?.Tick(deltaTime);
            if (_currentMode == CameraMode.Regional)
                ApplyCameraPriorities();
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
                InitializeModes();

            if (mode == CameraMode.Follow && _follow == null)
            {
                Debug.LogWarning("[SphericalCameraDirector] Follow 摄像机未配置。", this);
                return;
            }

            if (mode == CameraMode.Regional && _regional == null)
            {
                Debug.LogWarning("[SphericalCameraDirector] Regional 摄像机未配置。", this);
                return;
            }

            _currentMode = mode;
            _follow?.SetActive(mode == CameraMode.Follow);
            _regional?.SetActive(mode == CameraMode.Regional);

            if (mode == CameraMode.Regional)
                _regional?.Tick(0f);

            ApplyCameraPriorities();
            ApplyFieldOfView();
        }

        /// <summary>手动切换到 Follow 模式。</summary>
        public void UseFollowCamera() => SetMode(CameraMode.Follow);

        /// <summary>手动切换到 Regional 模式。</summary>
        public void UseRegionalCamera() => SetMode(CameraMode.Regional);

        private void InitializeModes()
        {
            if (_follow == null && _regional == null)
                ResolveReferences();

            SphereWalker walker = _centralCore != null ? _centralCore.SphereWalker : null;
            _follow?.Initialize(walker);
            _regional?.Initialize(walker);
            _follow?.SetBaseFieldOfView(_baseFieldOfView);
            _regional?.SetBaseFieldOfView(_baseFieldOfView);
            _initialized = _follow != null || _regional != null;
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

        private void ApplyCameraPriorities()
        {
            if (_follow?.Camera != null)
                _follow.Camera.Priority = _currentMode == CameraMode.Follow
                    ? _activePriority
                    : _inactivePriority;

            _regional?.SetPriority(_inactivePriority);
            if (_currentMode == CameraMode.Regional && _regional?.ActiveCamera != null)
                _regional.ActiveCamera.Priority = _activePriority;
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

            _follow?.SetFieldOfView(targetFieldOfView);
            _regional?.SetFieldOfView(targetFieldOfView);
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

            Enemy.AnyDamageApplied += HandleDamageApplied;

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

            Enemy.AnyDamageApplied -= HandleDamageApplied;

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

        private void HandleDamageApplied(Enemy _, DamageResult result)
        {
            if (result.WasApplied && !_runSettled && IsRunPlaying())
            {
                GenerateImpulse(result.WasKilled ? _deathImpulseForce : _damageImpulseForce);
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
            SetMode(_initialMode);
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
            _runSettled = state is GameState.Menu or GameState.Settled or GameState.Failed;
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

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
