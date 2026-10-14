using System;
using Cinemachine;
using UnityEngine;

namespace CameraSystem
{
    /// <summary>
    /// 驱动球面玩家周围的跟随摄像机。
    /// <para>相机沿稳定的球面切线方向环绕玩家；移动输入基底由编排器独立维护。</para>
    /// </summary>
    [DefaultExecutionOrder(-40)]
    [RequireComponent(typeof(CinemachineVirtualCamera))]
    public sealed class SphericalCameraFollow : MonoBehaviour
    {
        private const float MinimumDirectionMagnitude = 0.001f;
        private const float MinimumDirectionSqrMagnitude =
            MinimumDirectionMagnitude * MinimumDirectionMagnitude;
        private const float MinimumMovementSqrMagnitude = 0.000001f;
        private const float MinimumFocusWeight = 0.0001f;
        private const float MinimumCameraDistance = 1f;
        private const float DefaultCameraElevationDegrees = 70f;
        private const float MinimumCameraElevationDegrees = 15f;
        private const float MaximumCameraElevationDegrees = 85f;
        private const float MaximumOutwardAngleDegrees = 80f;

        private SphereWalker _walker;
        private CinemachineVirtualCamera _camera;
        private Vector3 _currentDirection;
        private Vector3 _poseDirection;
        private Vector3 _tangentUp;
        private Vector3 _lastPlayerPosition;
        private Vector3 _focusPosition;
        private float _focusWeight;
        private bool _hasFocusPosition;
        private bool _hasPose;
        private bool _initialized;
        private bool _active;

        /// <summary>获取由本脚本驱动的 Virtual Camera。</summary>
        public CinemachineVirtualCamera Camera => _camera;

        /// <summary>获取当前是否正在跟随。</summary>
        public bool IsActive => _active;

        /// <summary>初始化摄像机和球面跟随对象。</summary>
        /// <param name="walker">提供球心、球面半径和玩家位置的移动组件。</param>
        public void Initialize(SphereWalker walker)
        {
            _walker = walker;
            _camera = GetComponent<CinemachineVirtualCamera>();
            if (_camera == null)
            {
                return;
            }

            // 位置和朝向由本脚本生成，避免 Cinemachine Body/Aim 再次改写球面姿态。
            _camera.Follow = null;
            _camera.LookAt = null;
            EnsureImpulseListener();
            _initialized = _walker != null;
            if (_initialized)
            {
                ResetPose();
            }
        }

        /// <summary>启用或停用 Follow 模式。</summary>
        /// <param name="active">是否允许本组件更新摄像机姿态。</param>
        public void SetActive(bool active)
        {
            _active = active;
            if (_active && _initialized && !_hasPose)
            {
                ResetPose();
            }

            if (!_active)
            {
                _focusWeight = 0f;
                _hasFocusPosition = false;
            }
        }

        /// <summary>使用空配置推进跟随姿态，保留独立调用时的几何更新能力。</summary>
        /// <param name="deltaTime">本帧经过的缩放时间，单位为秒。</param>
        public void Tick(float deltaTime)
        {
            Tick(
                deltaTime,
                0f,
                0f,
                0f,
                0f,
                CameraFocusTarget.None,
                0f,
                0f,
                0f,
                0f,
                DefaultCameraElevationDegrees);
        }

        /// <summary>由 Director 在 LateUpdate 中推进跟随姿态。</summary>
        /// <param name="deltaTime">本帧经过的缩放时间，单位为秒。</param>
        /// <param name="cameraDistance">相机与玩家之间的目标距离。</param>
        /// <param name="followDelaySeconds">玩家移动时的方向跟随时间常数。</param>
        /// <param name="returnDelaySeconds">玩家停止时的方向回收时间常数。</param>
        /// <param name="lookAtVerticalOffset">沿球面切线向上的注视点偏移量。</param>
        /// <param name="focusTarget">当前战斗构图目标。</param>
        /// <param name="focusBlendInSeconds">锁定目标出现时的构图过渡时间。</param>
        /// <param name="focusBlendOutSeconds">锁定目标消失时的构图回收时间。</param>
        /// <param name="maximumFocusAngleDegrees">构图目标相对玩家法线允许偏移的最大角度。</param>
        public void Tick(
            float deltaTime,
            float cameraDistance,
            float followDelaySeconds,
            float returnDelaySeconds,
            float lookAtVerticalOffset,
            CameraFocusTarget focusTarget,
            float focusBlendInSeconds,
            float focusBlendOutSeconds,
            float maximumFocusAngleDegrees)
        {
            Tick(
                deltaTime,
                cameraDistance,
                followDelaySeconds,
                returnDelaySeconds,
                lookAtVerticalOffset,
                focusTarget,
                focusBlendInSeconds,
                focusBlendOutSeconds,
                maximumFocusAngleDegrees,
                0f);
        }

        /// <summary>更新相机与玩家的环绕距离及方位角。</summary>
        /// <param name="deltaTime">本帧经过的缩放时间，单位为秒。</param>
        /// <param name="cameraDistance">玩家到相机的目标距离。</param>
        /// <param name="followDelaySeconds">玩家移动时摄像机追随方向的平滑时间常数。</param>
        /// <param name="returnDelaySeconds">玩家停止时摄像机回归方向的平滑时间常数。</param>
        /// <param name="lookAtVerticalOffset">沿球面切线向上的注视点偏移量。</param>
        /// <param name="focusTarget">当前战斗构图目标。</param>
        /// <param name="focusBlendInSeconds">锁定目标出现时的构图过渡时间。</param>
        /// <param name="focusBlendOutSeconds">锁定目标消失时的构图回收时间。</param>
        /// <param name="maximumFocusAngleDegrees">构图目标相对玩家法线允许偏移的最大角度。</param>
        /// <param name="orbitAngleDegrees">相对玩家的水平环绕角度。</param>
        public void Tick(
            float deltaTime,
            float cameraDistance,
            float followDelaySeconds,
            float returnDelaySeconds,
            float lookAtVerticalOffset,
            CameraFocusTarget focusTarget,
            float focusBlendInSeconds,
            float focusBlendOutSeconds,
            float maximumFocusAngleDegrees,
            float orbitAngleDegrees)
        {
            Tick(
                deltaTime,
                cameraDistance,
                followDelaySeconds,
                returnDelaySeconds,
                lookAtVerticalOffset,
                focusTarget,
                focusBlendInSeconds,
                focusBlendOutSeconds,
                maximumFocusAngleDegrees,
                orbitAngleDegrees,
                DefaultCameraElevationDegrees);
        }

        /// <summary>更新玩家周围的相机距离、水平角和垂直角。</summary>
        /// <param name="deltaTime">本帧经过的缩放时间，单位为秒。</param>
        /// <param name="cameraDistance">玩家到相机的目标距离。</param>
        /// <param name="followDelaySeconds">玩家移动时摄像机追随方向的平滑时间常数。</param>
        /// <param name="returnDelaySeconds">玩家停止时摄像机回归方向的平滑时间常数。</param>
        /// <param name="lookAtVerticalOffset">沿球面切线向上的注视点偏移量。</param>
        /// <param name="focusTarget">当前战斗构图目标。</param>
        /// <param name="focusBlendInSeconds">锁定目标出现时的构图过渡时间。</param>
        /// <param name="focusBlendOutSeconds">锁定目标消失时的构图回收时间。</param>
        /// <param name="maximumFocusAngleDegrees">构图目标相对玩家法线允许偏移的最大角度。</param>
        /// <param name="orbitAngleDegrees">水平环绕方位角。</param>
        /// <param name="cameraElevationDegrees">相机相对玩家切平面的高度角。</param>
        public void Tick(
            float deltaTime,
            float cameraDistance,
            float followDelaySeconds,
            float returnDelaySeconds,
            float lookAtVerticalOffset,
            CameraFocusTarget focusTarget,
            float focusBlendInSeconds,
            float focusBlendOutSeconds,
            float maximumFocusAngleDegrees,
            float orbitAngleDegrees,
            float cameraElevationDegrees)
        {
            if (!_active || !_initialized || _walker == null || _camera == null)
            {
                return;
            }

            if (!IsFinite(deltaTime) || deltaTime <= 0f)
            {
                return;
            }

            Vector3 center = _walker.GetEffectiveCenter();
            Vector3 playerPosition = _walker.transform.position;
            Vector3 offset = playerPosition - center;
            if (!IsFinite(center) || !IsFinite(offset) ||
                !TryNormalize(offset, out Vector3 targetDirection))
            {
                return;
            }

            bool playerMoved = TryGetOffset(
                _lastPlayerPosition,
                playerPosition,
                out _,
                out float movementDistance) &&
                movementDistance > Mathf.Sqrt(MinimumMovementSqrMagnitude);
            float timeConstant = playerMoved ? followDelaySeconds : returnDelaySeconds;
            _currentDirection = SmoothDirection(
                _currentDirection,
                targetDirection,
                deltaTime,
                timeConstant);
            _lastPlayerPosition = playerPosition;

            // 沿摄像机方向运输切线向上，避免球面极点附近重新指北造成翻转。
            Quaternion transport = Quaternion.FromToRotation(_poseDirection, _currentDirection);
            Vector3 transportedUp = Vector3.ProjectOnPlane(
                transport * _tangentUp,
                _currentDirection);
            _tangentUp = TryNormalize(transportedUp, out Vector3 normalizedTransportedUp)
                ? normalizedTransportedUp
                : GetFallbackTangentUp(_currentDirection);
            _poseDirection = _currentDirection;

            float safeCameraDistance = IsFinite(cameraDistance)
                ? Mathf.Max(MinimumCameraDistance, cameraDistance)
                : MinimumCameraDistance;
            Vector3 orbitDirection = CalculateOrbitDirection(
                _currentDirection,
                _tangentUp,
                orbitAngleDegrees,
                cameraElevationDegrees);
            if (Vector3.Angle(targetDirection, orbitDirection) > MaximumOutwardAngleDegrees)
            {
                orbitDirection = Vector3.RotateTowards(
                    targetDirection,
                    orbitDirection,
                    MaximumOutwardAngleDegrees * Mathf.Deg2Rad,
                    0f);
            }

            transform.position = AddFinite(
                playerPosition,
                orbitDirection * safeCameraDistance,
                playerPosition + targetDirection * safeCameraDistance);

            UpdateFocus(
                deltaTime,
                focusTarget,
                focusBlendInSeconds,
                focusBlendOutSeconds);
            CameraFocusMode lookMode = _hasFocusPosition
                ? CameraFocusMode.LockedTarget
                : CameraFocusMode.None;
            Vector3 lookTarget = CalculateLookTarget(
                center,
                playerPosition,
                _focusPosition,
                lookMode,
                _focusWeight,
                maximumFocusAngleDegrees,
                _tangentUp,
                lookAtVerticalOffset);
            if (TryNormalize(lookTarget - transform.position, out Vector3 lookDirection))
            {
                Vector3 lookUp = Vector3.ProjectOnPlane(_tangentUp, lookDirection);
                if (!TryNormalize(lookUp, out lookUp))
                {
                    lookUp = GetFallbackTangentUp(_currentDirection);
                }

                transform.rotation = Quaternion.LookRotation(lookDirection, lookUp);
            }
        }

        /// <summary>设置当前 Follow 摄像机的运行时 FOV。</summary>
        /// <param name="fieldOfView">垂直视野角，单位为度。</param>
        public void SetFieldOfView(float fieldOfView)
        {
            if (_camera == null || !IsFinite(fieldOfView))
            {
                return;
            }

            LensSettings lens = _camera.m_Lens;
            lens.FieldOfView = fieldOfView;
            _camera.m_Lens = lens;
        }

        /// <summary>计算外层球面上的摄像机位置。</summary>
        /// <param name="center">球心世界坐标。</param>
        /// <param name="direction">从球心指向摄像机的方向。</param>
        /// <param name="radius">摄像机到球心的距离。</param>
        /// <returns>球面外层的摄像机位置。</returns>
        public static Vector3 CalculateCameraPosition(Vector3 center, Vector3 direction, float radius)
        {
            if (!IsFinite(center))
            {
                return Vector3.zero;
            }

            if (!TryNormalize(direction, out Vector3 safeDirection))
            {
                return center;
            }

            float safeRadius = IsFinite(radius) ? Mathf.Max(0f, radius) : 0f;
            return AddFinite(center, safeDirection * safeRadius, center);
        }

        /// <summary>计算位于球体外侧的玩家环绕方向。</summary>
        /// <param name="surfaceNormal">摄像机平滑跟随的球面法线。</param>
        /// <param name="tangentUp">已运输的球面切线上方向。</param>
        /// <param name="orbitAngleDegrees">相对切线基底的环绕角度。</param>
        /// <param name="cameraElevationDegrees">相对球面切平面的高度角。</param>
        /// <returns>朝球面外侧且包含水平环绕角度的单位向量。</returns>
        private static Vector3 CalculateOrbitDirection(
            Vector3 surfaceNormal,
            Vector3 tangentUp,
            float orbitAngleDegrees,
            float cameraElevationDegrees)
        {
            if (!TryNormalize(surfaceNormal, out Vector3 normal))
            {
                return Vector3.forward;
            }

            Vector3 up = Vector3.ProjectOnPlane(tangentUp, normal);
            if (!TryNormalize(up, out up))
            {
                up = GetFallbackTangentUp(normal);
            }

            Vector3 tangentRight = Vector3.Cross(up, normal);
            if (!TryNormalize(tangentRight, out tangentRight))
            {
                return normal;
            }

            float safeOrbitAngle = IsFinite(orbitAngleDegrees) ? orbitAngleDegrees : 0f;
            Vector3 tangentDirection = Quaternion.AngleAxis(safeOrbitAngle, normal) * tangentRight;
            float safeElevation = IsFinite(cameraElevationDegrees)
                ? Mathf.Clamp(
                    cameraElevationDegrees,
                    MinimumCameraElevationDegrees,
                    MaximumCameraElevationDegrees)
                : DefaultCameraElevationDegrees;
            float elevation = safeElevation * Mathf.Deg2Rad;
            Vector3 offsetDirection = normal * Mathf.Sin(elevation) +
                tangentDirection * Mathf.Cos(elevation);

            return TryNormalize(offsetDirection, out Vector3 normalizedDirection)
                ? normalizedDirection
                : normal;
        }

        /// <summary>
        /// 按时间常数平滑球面方向，结果不会越过目标方向。
        /// </summary>
        /// <param name="currentDirection">当前单位方向。</param>
        /// <param name="targetDirection">目标单位方向。</param>
        /// <param name="deltaTime">本帧经过的时间，单位为秒。</param>
        /// <param name="timeConstant">指数平滑时间常数，单位为秒。</param>
        /// <returns>平滑后的单位方向。</returns>
        public static Vector3 SmoothDirection(
            Vector3 currentDirection,
            Vector3 targetDirection,
            float deltaTime,
            float timeConstant)
        {
            bool hasTarget = TryNormalize(targetDirection, out Vector3 safeTargetDirection);
            bool hasCurrent = TryNormalize(currentDirection, out Vector3 safeCurrentDirection);
            if (!hasTarget)
            {
                return hasCurrent ? safeCurrentDirection : Vector3.forward;
            }

            if (!hasCurrent)
            {
                return safeTargetDirection;
            }

            float safeDeltaTime = IsFinite(deltaTime) ? Mathf.Max(0f, deltaTime) : 0f;
            float safeTimeConstant = IsFinite(timeConstant)
                ? Mathf.Max(0.001f, timeConstant)
                : 0.001f;
            float smoothing = 1f - Mathf.Exp(-safeDeltaTime / safeTimeConstant);
            Vector3 smoothedDirection = Vector3.RotateTowards(
                safeCurrentDirection,
                safeTargetDirection,
                Vector3.Angle(safeCurrentDirection, safeTargetDirection) * smoothing * Mathf.Deg2Rad,
                0f);
            return TryNormalize(smoothedDirection, out Vector3 result)
                ? result
                : safeTargetDirection;
        }

        /// <summary>按时间常数平滑一个无方向标量。</summary>
        /// <param name="currentValue">当前值。</param>
        /// <param name="targetValue">目标值。</param>
        /// <param name="deltaTime">本帧经过的时间，单位为秒。</param>
        /// <param name="timeConstant">指数平滑时间常数，单位为秒。</param>
        /// <returns>平滑后的值。</returns>
        public static float SmoothValue(
            float currentValue,
            float targetValue,
            float deltaTime,
            float timeConstant)
        {
            if (!IsFinite(currentValue))
            {
                return IsFinite(targetValue) ? targetValue : 0f;
            }

            if (!IsFinite(targetValue) || !IsFinite(deltaTime) || deltaTime <= 0f)
            {
                return currentValue;
            }

            if (Mathf.Approximately(currentValue, targetValue))
            {
                return targetValue;
            }

            if (!IsFinite(timeConstant) || timeConstant <= 0f)
            {
                return targetValue;
            }

            float smoothing = 1f - Mathf.Exp(-deltaTime / timeConstant);
            return LerpFinite(currentValue, targetValue, smoothing, targetValue);
        }

        /// <summary>
        /// 计算玩家与锁定目标之间的有限构图注视点。
        /// </summary>
        /// <param name="center">球心世界坐标。</param>
        /// <param name="playerPosition">玩家世界坐标。</param>
        /// <param name="focusPosition">锁定目标世界坐标。</param>
        /// <param name="focusMode">当前构图模式。</param>
        /// <param name="focusWeight">锁定目标混合权重，范围为零到一。</param>
        /// <param name="maximumFocusAngleDegrees">目标方向相对玩家方向允许偏移的最大角度。</param>
        /// <param name="tangentUp">球面切线向上方向。</param>
        /// <param name="verticalOffset">沿切线向上的额外偏移量。</param>
        /// <returns>摄像机应注视的世界坐标。</returns>
        public static Vector3 CalculateLookTarget(
            Vector3 center,
            Vector3 playerPosition,
            Vector3 focusPosition,
            CameraFocusMode focusMode,
            float focusWeight,
            float maximumFocusAngleDegrees,
            Vector3 tangentUp,
            float verticalOffset)
        {
            if (!IsFinite(center) || !IsFinite(playerPosition))
            {
                return IsFinite(center) ? center : Vector3.zero;
            }

            Vector3 safeTangentUp = TryNormalize(tangentUp, out Vector3 normalizedTangentUp)
                ? normalizedTangentUp
                : Vector3.up;
            float safeVerticalOffset = IsFinite(verticalOffset) ? verticalOffset : 0f;
            Vector3 baseTarget = AddFinite(
                playerPosition,
                safeTangentUp * safeVerticalOffset,
                playerPosition);
            if (focusMode != CameraFocusMode.LockedTarget ||
                !IsFinite(focusWeight) ||
                focusWeight <= 0f ||
                !IsFinite(focusPosition))
            {
                return baseTarget;
            }

            if (!TryGetOffset(
                    center,
                    playerPosition,
                    out Vector3 playerDirection,
                    out _) ||
                !TryGetOffset(
                    center,
                    focusPosition,
                    out Vector3 focusDirection,
                    out float focusDistance))
            {
                return baseTarget;
            }

            float safeMaximumAngle = IsFinite(maximumFocusAngleDegrees)
                ? Mathf.Clamp(maximumFocusAngleDegrees, 0f, 89f)
                : 0f;
            Vector3 constrainedDirection = Vector3.RotateTowards(
                playerDirection,
                focusDirection,
                safeMaximumAngle * Mathf.Deg2Rad,
                0f);
            if (!TryNormalize(constrainedDirection, out constrainedDirection))
            {
                return baseTarget;
            }

            Vector3 constrainedFocusPosition = AddFinite(
                center,
                constrainedDirection * focusDistance,
                focusPosition);
            Vector3 target = LerpFinite(
                playerPosition,
                constrainedFocusPosition,
                Mathf.Clamp01(focusWeight),
                playerPosition);
            return AddFinite(target, safeTangentUp * safeVerticalOffset, target);
        }

        private void ResetPose()
        {
            Vector3 playerPosition = _walker.transform.position;
            Vector3 center = _walker.GetEffectiveCenter();
            Vector3 offset = playerPosition - center;
            _currentDirection = TryNormalize(offset, out Vector3 normalizedDirection)
                ? normalizedDirection
                : Vector3.forward;
            _lastPlayerPosition = playerPosition;
            _poseDirection = _currentDirection;
            _tangentUp = GetFallbackTangentUp(_currentDirection);
            _focusPosition = playerPosition;
            _focusWeight = 0f;
            _hasFocusPosition = false;
            _hasPose = true;
        }

        private void UpdateFocus(
            float deltaTime,
            CameraFocusTarget focusTarget,
            float focusBlendInSeconds,
            float focusBlendOutSeconds)
        {
            bool hasTarget = focusTarget.Mode == CameraFocusMode.LockedTarget &&
                IsFinite(focusTarget.Position) &&
                IsFinite(focusTarget.Weight);
            float targetWeight = hasTarget ? Mathf.Clamp01(focusTarget.Weight) : 0f;
            float blendTime = hasTarget ? focusBlendInSeconds : focusBlendOutSeconds;
            _focusWeight = SmoothValue(
                _focusWeight,
                targetWeight,
                deltaTime,
                Mathf.Max(0.001f, IsFinite(blendTime) ? blendTime : 0.001f));

            if (hasTarget)
            {
                _focusPosition = _hasFocusPosition
                    ? SmoothPosition(_focusPosition, focusTarget.Position, deltaTime, focusBlendInSeconds)
                    : focusTarget.Position;
                _hasFocusPosition = true;
                return;
            }

            if (_focusWeight <= MinimumFocusWeight)
            {
                _focusWeight = 0f;
                _hasFocusPosition = false;
            }
        }

        private static Vector3 SmoothPosition(
            Vector3 currentPosition,
            Vector3 targetPosition,
            float deltaTime,
            float timeConstant)
        {
            if (!IsFinite(currentPosition) ||
                !IsFinite(targetPosition) ||
                !IsFinite(deltaTime) ||
                deltaTime <= 0f)
            {
                return IsFinite(currentPosition) ? currentPosition : targetPosition;
            }

            float safeTimeConstant = IsFinite(timeConstant)
                ? Mathf.Max(0.001f, timeConstant)
                : 0.001f;
            float smoothing = 1f - Mathf.Exp(-deltaTime / safeTimeConstant);
            return LerpFinite(
                currentPosition,
                targetPosition,
                smoothing,
                currentPosition);
        }

        private static Vector3 GetFallbackTangentUp(Vector3 direction)
        {
            Vector3 safeDirection = TryNormalize(direction, out Vector3 normalizedDirection)
                ? normalizedDirection
                : Vector3.forward;
            SphericalCoordinates.GetTangentBasis(safeDirection, out _, out Vector3 tangentUp);
            return TryNormalize(tangentUp, out Vector3 normalizedTangentUp)
                ? normalizedTangentUp
                : Vector3.up;
        }

        private void EnsureImpulseListener()
        {
            if (GetComponent<CinemachineImpulseListener>() == null)
            {
                gameObject.AddComponent<CinemachineImpulseListener>();
            }
        }

        private static bool TryNormalize(Vector3 value, out Vector3 normalized)
        {
            normalized = Vector3.zero;
            if (!IsFinite(value))
            {
                return false;
            }

            float scale = Mathf.Max(
                Mathf.Abs(value.x),
                Mathf.Abs(value.y),
                Mathf.Abs(value.z));
            if (!IsFinite(scale) || scale <= 0f)
            {
                return false;
            }

            Vector3 scaled = value / scale;
            float magnitude = Mathf.Sqrt(Vector3.Dot(scaled, scaled));
            if (!IsFinite(magnitude) || magnitude <= 0f ||
                scale < MinimumDirectionMagnitude / magnitude)
            {
                return false;
            }

            normalized = scaled / magnitude;
            return IsFinite(normalized);
        }

        private static bool TryGetOffset(
            Vector3 from,
            Vector3 to,
            out Vector3 direction,
            out float distance)
        {
            direction = Vector3.zero;
            distance = 0f;
            if (!IsFinite(from) || !IsFinite(to))
            {
                return false;
            }

            double x = (double)to.x - from.x;
            double y = (double)to.y - from.y;
            double z = (double)to.z - from.z;
            double magnitude = Math.Sqrt(x * x + y * y + z * z);
            if (double.IsNaN(magnitude) || double.IsInfinity(magnitude) ||
                magnitude < MinimumDirectionMagnitude)
            {
                return false;
            }

            direction = new Vector3(
                (float)(x / magnitude),
                (float)(y / magnitude),
                (float)(z / magnitude));
            distance = magnitude >= float.MaxValue
                ? float.MaxValue
                : (float)magnitude;
            return IsFinite(direction) && IsFinite(distance);
        }

        private static Vector3 AddFinite(
            Vector3 first,
            Vector3 second,
            Vector3 fallback)
        {
            Vector3 result = first + second;
            return IsFinite(result)
                ? result
                : IsFinite(fallback) ? fallback : Vector3.zero;
        }

        private static Vector3 LerpFinite(
            Vector3 from,
            Vector3 to,
            float weight,
            Vector3 fallback)
        {
            if (!IsFinite(from) || !IsFinite(to) || !IsFinite(weight))
            {
                return IsFinite(fallback) ? fallback : Vector3.zero;
            }

            float safeWeight = Mathf.Clamp01(weight);
            Vector3 result = new Vector3(
                (float)((double)from.x + (to.x - (double)from.x) * safeWeight),
                (float)((double)from.y + (to.y - (double)from.y) * safeWeight),
                (float)((double)from.z + (to.z - (double)from.z) * safeWeight));
            return IsFinite(result)
                ? result
                : IsFinite(fallback) ? fallback : Vector3.zero;
        }

        private static float LerpFinite(
            float from,
            float to,
            float weight,
            float fallback)
        {
            if (!IsFinite(from) || !IsFinite(to) || !IsFinite(weight))
            {
                return IsFinite(fallback) ? fallback : 0f;
            }

            float safeWeight = Mathf.Clamp01(weight);
            float result = (float)((double)from +
                (to - (double)from) * safeWeight);
            return IsFinite(result)
                ? result
                : IsFinite(fallback) ? fallback : 0f;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
