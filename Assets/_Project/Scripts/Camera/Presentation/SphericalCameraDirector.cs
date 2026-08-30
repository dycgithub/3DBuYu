using System;
using Cinemachine;
using GameSystem;
using R3;
using Services;
using UnityEngine;
using VContainer;

namespace CameraSystem
{
    /// <summary>
    /// 为球面战斗场景驱动单台 FreeLook 摄影机的表现状态。
    /// <para>本类只调整镜头轨道、FOV 和冲击，不接管移动、瞄准或攻击输入。</para>
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [RequireComponent(typeof(CinemachineFreeLook))]
    public sealed class SphericalCameraDirector : MonoBehaviour
    {
        [Header("镜头")]
        [SerializeField] private CinemachineFreeLook _freeLook;
        [SerializeField] private CinemachineImpulseSource _impulseSource;
        [SerializeField, Range(0.1f, 0.9f)] private float _verticalAxisValue = 0.5f;
        [SerializeField, Min(0f)] private float _blendSpeed = 3f;
        [SerializeField, Min(0f)] private float _lensBlendSpeed = 24f;

        [Header("敌群压力")]
        [SerializeField, Min(1)] private int _pressureEnemyCount = 12;
        [SerializeField, Min(0f)] private float _pressureFovIncrease = 8f;
        [SerializeField, Range(0f, 1f)] private float _pressureOrbitScale = 0.25f;

        [Header("波次提示")]
        [SerializeField, Min(0f)] private float _waveIntroDuration = 1.2f;
        [SerializeField, Min(0f)] private float _waveIntroFovIncrease = 6f;
        [SerializeField, Range(0f, 1f)] private float _waveIntroOrbitScale = 0.4f;
        [SerializeField, Min(0f)] private float _waveImpulseForce = 0.12f;

        [Header("连杀冲击")]
        [SerializeField, Min(1)] private int _streakImpulseThreshold = 3;
        [SerializeField, Min(1)] private int _streakImpulseCap = 10;
        [SerializeField, Min(0f)] private float _streakMinImpulseForce = 0.08f;
        [SerializeField, Min(0f)] private float _streakMaxImpulseForce = 0.22f;

        [Inject] private IWaveEventService _waveService;
        [Inject] private IKillStreakService _killStreak;
        [Inject] private GameManager _gameManager;

        private CinemachineFreeLook.Orbit[] _baseOrbits;
        private float _baseFieldOfView;
        private float _pressureWeight;
        private float _waveIntroTimer;
        private int _enemiesAlive;
        private int _lastStreak;
        private bool _runSettled;

        private IDisposable _waveStartedSubscription;
        private IDisposable _enemyCountSubscription;
        private IDisposable _streakSubscription;
        private bool _gameEventsSubscribed;

        private void Awake()
        {
            if (_freeLook == null)
                _freeLook = GetComponent<CinemachineFreeLook>();
            if (_impulseSource == null)
                _impulseSource = GetComponent<CinemachineImpulseSource>();

            if (_freeLook == null)
            {
                Debug.LogWarning("[SphericalCameraDirector] 未找到 CinemachineFreeLook。", this);
                enabled = false;
                return;
            }

            _baseFieldOfView = _freeLook.m_Lens.FieldOfView;
            _baseOrbits = CopyOrbits(_freeLook.m_Orbits);
            DisableUserCameraInput();
        }

        private void Start()
        {
            if (_freeLook == null)
                return;

            if (_waveService != null)
            {
                _enemiesAlive = Mathf.Max(0, _waveService.EnemiesAlive);
                _waveStartedSubscription = _waveService.OnWaveStarted.Subscribe(HandleWaveStarted);
                _enemyCountSubscription = _waveService.OnEnemiesAliveChanged.Subscribe(HandleEnemyCountChanged);
            }
            else
            {
                Debug.LogWarning("[SphericalCameraDirector] 未注入 IWaveEventService，敌群压力镜头不会生效。", this);
            }

            if (_killStreak != null)
                _streakSubscription = _killStreak.CurrentStreak.Subscribe(HandleStreakChanged);
            else
                Debug.LogWarning("[SphericalCameraDirector] 未注入 IKillStreakService，连杀冲击不会生效。", this);

            if (_gameManager != null)
            {
                _gameManager.OnGameStarted += HandleRunStarted;
                _gameManager.OnSettled += HandleRunSettled;
                _gameEventsSubscribed = true;
            }
        }

        private void Update()
        {
            if (_freeLook == null)
                return;

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime))
                return;

            _waveIntroTimer = Mathf.Max(0f, _waveIntroTimer - deltaTime);

            float pressureTarget = _runSettled
                ? 0f
                : Mathf.Clamp01(_enemiesAlive / (float)Mathf.Max(1, _pressureEnemyCount));
            _pressureWeight = Mathf.MoveTowards(
                _pressureWeight,
                pressureTarget,
                Mathf.Max(0f, _blendSpeed) * deltaTime);

            float waveWeight = GetWaveIntroWeight();
            float targetFieldOfView = _baseFieldOfView
                + _pressureWeight * Mathf.Max(0f, _pressureFovIncrease)
                + waveWeight * Mathf.Max(0f, _waveIntroFovIncrease);
            _freeLook.m_Lens.FieldOfView = Mathf.MoveTowards(
                _freeLook.m_Lens.FieldOfView,
                targetFieldOfView,
                Mathf.Max(0f, _lensBlendSpeed) * deltaTime);

            float orbitScale = 1f
                + _pressureWeight * Mathf.Clamp01(_pressureOrbitScale)
                + waveWeight * Mathf.Clamp01(_waveIntroOrbitScale);
            ApplyOrbitScale(orbitScale);
        }

        private void OnDestroy()
        {
            _waveStartedSubscription?.Dispose();
            _enemyCountSubscription?.Dispose();
            _streakSubscription?.Dispose();

            if (_gameEventsSubscribed && _gameManager != null)
            {
                _gameManager.OnGameStarted -= HandleRunStarted;
                _gameManager.OnSettled -= HandleRunSettled;
            }
        }

        private void DisableUserCameraInput()
        {
            _freeLook.m_BindingMode = CinemachineOrbitalTransposer.BindingMode.LockToTarget;
            _freeLook.m_XAxis.m_InputAxisName = string.Empty;
            _freeLook.m_YAxis.m_InputAxisName = string.Empty;
            _freeLook.m_XAxis.m_InputAxisValue = 0f;
            _freeLook.m_YAxis.m_InputAxisValue = 0f;
            _freeLook.m_XAxis.Value = 0f;
            _freeLook.m_YAxis.Value = Mathf.Clamp01(_verticalAxisValue);
            _freeLook.m_XAxis.m_Recentering.m_enabled = false;
            _freeLook.m_YAxis.m_Recentering.m_enabled = false;
            _freeLook.m_RecenterToTargetHeading.m_enabled = false;
            _freeLook.m_YAxisRecentering.m_enabled = false;
        }

        private void HandleWaveStarted(int waveNumber)
        {
            if (_runSettled)
                return;

            _waveIntroTimer = Mathf.Max(_waveIntroTimer, Mathf.Max(0f, _waveIntroDuration));
            GenerateImpulse(_waveImpulseForce);
        }

        private void HandleEnemyCountChanged(int enemiesAlive)
        {
            _enemiesAlive = Mathf.Max(0, enemiesAlive);
        }

        private void HandleStreakChanged(int streak)
        {
            int normalizedStreak = Mathf.Max(0, streak);
            if (!_runSettled && normalizedStreak > _lastStreak && normalizedStreak >= _streakImpulseThreshold)
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

        private void HandleRunStarted()
        {
            _runSettled = false;
            _pressureWeight = 0f;
            _waveIntroTimer = 0f;
            _lastStreak = 0;
            _enemiesAlive = _waveService != null ? Mathf.Max(0, _waveService.EnemiesAlive) : 0;
        }

        private void HandleRunSettled(bool _, int __)
        {
            _runSettled = true;
            _waveIntroTimer = 0f;
        }

        private float GetWaveIntroWeight()
        {
            float duration = Mathf.Max(0f, _waveIntroDuration);
            if (duration <= 0f)
                return 0f;

            float normalized = Mathf.Clamp01(_waveIntroTimer / duration);
            return normalized * normalized * (3f - 2f * normalized);
        }

        private void ApplyOrbitScale(float scale)
        {
            if (_baseOrbits == null || _freeLook.m_Orbits == null ||
                _freeLook.m_Orbits.Length != _baseOrbits.Length)
                return;

            for (int i = 0; i < _baseOrbits.Length; i++)
            {
                CinemachineFreeLook.Orbit orbit = _baseOrbits[i];
                orbit.m_Height *= scale;
                orbit.m_Radius *= scale;
                _freeLook.m_Orbits[i] = orbit;
            }
        }

        private void GenerateImpulse(float force)
        {
            if (_impulseSource == null || force <= 0f)
                return;

            Vector3 origin = _freeLook != null && _freeLook.Follow != null
                ? _freeLook.Follow.position
                : transform.position;
            _impulseSource.GenerateImpulseAtPositionWithVelocity(origin, Vector3.down * force);
        }

        private static CinemachineFreeLook.Orbit[] CopyOrbits(CinemachineFreeLook.Orbit[] source)
        {
            if (source == null)
                return Array.Empty<CinemachineFreeLook.Orbit>();

            var copy = new CinemachineFreeLook.Orbit[source.Length];
            Array.Copy(source, copy, source.Length);
            return copy;
        }

        private void OnValidate()
        {
            _pressureEnemyCount = Mathf.Max(1, _pressureEnemyCount);
            _streakImpulseThreshold = Mathf.Max(1, _streakImpulseThreshold);
            _streakImpulseCap = Mathf.Max(_streakImpulseThreshold, _streakImpulseCap);
            _blendSpeed = Mathf.Max(0f, _blendSpeed);
            _lensBlendSpeed = Mathf.Max(0f, _lensBlendSpeed);
            _waveIntroDuration = Mathf.Max(0f, _waveIntroDuration);
        }
    }
}
