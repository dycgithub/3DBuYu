using Cinemachine;
using DG.Tweening;
using UnityEngine;

namespace CameraSystem
{
    /// <summary>
    /// 驱动球面战斗的跟随摄像机。
    /// <para>在外层球面上平滑追赶玩家，画面上方随法线连续运输，跨极点时保持朝向连续。</para>
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [RequireComponent(typeof(CinemachineVirtualCamera))]
    public sealed class SphericalCameraFollow : MonoBehaviour
    {
        private const float MinimumDirectionSqrMagnitude = 0.000001f;
        private const float MinimumMovementSqrMagnitude = 0.000001f;
        private const float MinimumFieldOfView = 1f;
        private const float MaximumFieldOfView = 179f;
        private const float DefaultFieldOfView = 61f;

        [Header("跟随对象")]
        [SerializeField] private SphereWalker _walker;

        [Header("球面跟随")]
        [SerializeField, Min(0f)] private float _cameraDistanceOffset = 12.5f;
        [SerializeField, Min(0.001f)] private float _followDelaySeconds = 0.2f;
        [SerializeField, Min(0.001f)] private float _returnDelaySeconds = 0.1f;

        [Header("构图")]
        [SerializeField, Min(MinimumFieldOfView)] private float _baseFieldOfView = DefaultFieldOfView;
        [SerializeField] private float _lookAtVerticalOffset;

        private CinemachineVirtualCamera _camera;
        private Vector3 _currentDirection;
        private Vector3 _poseDirection;
        private Vector3 _tangentUp;
        private Vector3 _lastPlayerPosition;
        private Vector3 _tweenStartDirection;
        private Vector3 _tweenTargetDirection;
        private float _directionTweenProgress;
        private float _directionTweenDuration;
        private Tweener _directionTween;
        private bool _hasPose;
        private bool _initialized;
        private bool _active;

        /// <summary>获取由本脚本驱动的 Virtual Camera。</summary>
        public CinemachineVirtualCamera Camera => _camera;

        /// <summary>获取当前是否正在跟随。</summary>
        public bool IsActive => _active;

        /// <summary>初始化摄像机和球面跟随对象。</summary>
        public void Initialize(SphereWalker walker)
        {
            if (walker != null)
                _walker = walker;

            _camera = GetComponent<CinemachineVirtualCamera>();
            if (_camera == null)
                return;

            // 位置和朝向由本脚本生成，避免 Cinemachine Body/Aim 再次改写球面姿态。
            _camera.Follow = null;
            _camera.LookAt = null;
            EnsureImpulseListener();
            _camera.m_Lens.FieldOfView = NormalizeFieldOfView(_baseFieldOfView);
            _initialized = _walker != null;
            if (_initialized)
            {
                ResetPose();
            }

            EnsureDirectionTween();
        }

        /// <summary>启用或停用 Follow 模式。</summary>
        public void SetActive(bool active)
        {
            _active = active;
            if (_active && _initialized && !_hasPose)
            {
                ResetPose();
            }

            if (_active)
            {
                _directionTween?.Play();
            }
            else
            {
                _directionTween?.Pause();
            }
        }

        /// <summary>由 Director 在 LateUpdate 中推进跟随姿态。</summary>
        public void Tick(float deltaTime)
        {
            if (!_active || !_initialized || _walker == null || _camera == null)
                return;
            if (!IsFinite(deltaTime) || deltaTime <= 0f)
                return;

            Vector3 center = _walker.GetEffectiveCenter();
            Vector3 offset = _walker.transform.position - center;
            if (offset.sqrMagnitude < MinimumDirectionSqrMagnitude)
                return;

            Vector3 targetDirection = offset.normalized;
            bool playerMoved = (_walker.transform.position - _lastPlayerPosition).sqrMagnitude >
                MinimumMovementSqrMagnitude;
            float timeConstant = playerMoved ? _followDelaySeconds : _returnDelaySeconds;
            RetargetDirection(targetDirection, timeConstant);
            _lastPlayerPosition = _walker.transform.position;

            float radius = Mathf.Max(0.01f, _walker.radius) + _cameraDistanceOffset;
            transform.position = CalculateCameraPosition(center, _currentDirection, radius);

            // 保留沿路径积累的朝向，避免在极点附近重新指北造成画面翻转。
            Quaternion transport = Quaternion.FromToRotation(_poseDirection, _currentDirection);
            _tangentUp = Vector3.ProjectOnPlane(transport * _tangentUp, _currentDirection).normalized;
            _poseDirection = _currentDirection;
            Vector3 lookTarget = _walker.transform.position + _tangentUp * _lookAtVerticalOffset;
            Vector3 lookDirection = lookTarget - transform.position;
            if (lookDirection.sqrMagnitude > MinimumDirectionSqrMagnitude)
            {
                transform.rotation = Quaternion.LookRotation(lookDirection, _tangentUp);
            }
        }

        /// <summary>设置 Follow 摄像机的基础 FOV。</summary>
        public void SetBaseFieldOfView(float fieldOfView)
        {
            _baseFieldOfView = NormalizeFieldOfView(fieldOfView);
            if (_camera != null)
                _camera.m_Lens.FieldOfView = _baseFieldOfView;
        }

        /// <summary>设置当前 Follow 摄像机的运行时 FOV。</summary>
        public void SetFieldOfView(float fieldOfView)
        {
            if (_camera == null)
                return;

            LensSettings lens = _camera.m_Lens;
            lens.FieldOfView = NormalizeFieldOfView(fieldOfView);
            _camera.m_Lens = lens;
        }

        /// <summary>计算外层球面上的摄像机位置。</summary>
        public static Vector3 CalculateCameraPosition(Vector3 center, Vector3 direction, float radius)
        {
            if (direction.sqrMagnitude < MinimumDirectionSqrMagnitude)
                return center;

            return center + direction.normalized * Mathf.Max(0f, radius);
        }

        /// <summary>按时间常数平滑旋转方向，避免直接平滑世界坐标破坏球面约束。</summary>
        public static Vector3 SmoothDirection(
            Vector3 currentDirection,
            Vector3 targetDirection,
            float deltaTime,
            float timeConstant)
        {
            if (targetDirection.sqrMagnitude < MinimumDirectionSqrMagnitude)
                return currentDirection.sqrMagnitude < MinimumDirectionSqrMagnitude
                    ? Vector3.forward
                    : currentDirection.normalized;
            if (currentDirection.sqrMagnitude < MinimumDirectionSqrMagnitude)
                return targetDirection.normalized;

            float smoothing = 1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) /
                Mathf.Max(0.001f, timeConstant));
            return Vector3.RotateTowards(
                currentDirection.normalized,
                targetDirection.normalized,
                Vector3.Angle(currentDirection, targetDirection) * smoothing * Mathf.Deg2Rad,
                0f).normalized;
        }

        private void ResetPose()
        {
            Vector3 center = _walker.GetEffectiveCenter();
            Vector3 offset = _walker.transform.position - center;
            _currentDirection = offset.sqrMagnitude >= MinimumDirectionSqrMagnitude
                ? offset.normalized
                : Vector3.forward;
            _lastPlayerPosition = _walker.transform.position;
            _tweenStartDirection = _currentDirection;
            _poseDirection = _currentDirection;
            SphericalCoordinates.GetTangentBasis(_currentDirection, out _, out _tangentUp);
            _tweenTargetDirection = _currentDirection;
            _directionTweenProgress = 1f;
            _hasPose = true;
        }

        private void EnsureDirectionTween()
        {
            if (_directionTween != null)
            {
                return;
            }

            _directionTween = DOTween.To(
                    () => _directionTweenProgress,
                    progress =>
                    {
                        _directionTweenProgress = progress;
                        _currentDirection = Vector3.Slerp(
                            _tweenStartDirection,
                            _tweenTargetDirection,
                            progress).normalized;
                    },
                    1f,
                    Mathf.Max(0.001f, _directionTweenDuration))
                .SetEase(Ease.Linear)
                .SetAutoKill(false)
                .SetUpdate(UpdateType.Late)
                .Pause();
        }

        private void RetargetDirection(Vector3 targetDirection, float timeConstant)
        {
            EnsureDirectionTween();
            float duration = Mathf.Max(0.001f, timeConstant);
            bool targetChanged = Vector3.Angle(_tweenTargetDirection, targetDirection) > 0.001f;
            bool durationChanged = !Mathf.Approximately(_directionTweenDuration, duration);
            if (!targetChanged && !durationChanged && _directionTween.IsActive())
            {
                if (!_directionTween.IsPlaying() && _directionTweenProgress < 1f)
                {
                    _directionTween.Play();
                }

                return;
            }

            _tweenStartDirection = _currentDirection;
            _tweenTargetDirection = targetDirection;
            _directionTweenDuration = duration;
            _directionTweenProgress = 0f;
            _directionTween.ChangeEndValue(1f, duration, true).Restart();
        }

        private void EnsureImpulseListener()
        {
            if (GetComponent<CinemachineImpulseListener>() == null)
                gameObject.AddComponent<CinemachineImpulseListener>();
        }

        private void OnDestroy()
        {
            _directionTween?.Kill();
            _directionTween = null;
        }

        private void OnValidate()
        {
            _cameraDistanceOffset = NormalizeNonNegative(_cameraDistanceOffset);
            _followDelaySeconds = NormalizePositive(_followDelaySeconds);
            _returnDelaySeconds = NormalizePositive(_returnDelaySeconds);
            _baseFieldOfView = NormalizeFieldOfView(_baseFieldOfView);
        }

        private static float NormalizePositive(float value)
        {
            return IsFinite(value) ? Mathf.Max(0.001f, value) : 0.2f;
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
