using System;
using System.Collections.Generic;
using Cinemachine;
using UnityEngine;

namespace CameraSystem
{
    /// <summary>
    /// 管理固定在外层球面上的区域摄像机。
    /// <para>本类只负责区域选择和位置计算，可调参数由 SphericalCameraDirector 统一注入。</para>
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class SphericalCameraRegional : MonoBehaviour
    {
        private const float MinimumDirectionSqrMagnitude = 0.000001f;
        private const float MaximumCoverageRadius = 89f;
        private const float MinimumCoverageCandidateSqrMagnitude = 0.000001f;

        private SphereWalker _walker;
        private CinemachineVirtualCamera[] _regionCameras = Array.Empty<CinemachineVirtualCamera>();
        private Vector3[] _regionDirections = Array.Empty<Vector3>();
        private float _calculatedCoverageRadiusDegrees;
        private int _currentRegionIndex = -1;
        private bool _initialized;
        private bool _active;

        /// <summary>获取所有区域摄像机。</summary>
        public CinemachineVirtualCamera[] RegionCameras => _regionCameras;

        /// <summary>获取当前区域索引，未选中时为 -1。</summary>
        public int CurrentRegionIndex => _currentRegionIndex;

        /// <summary>获取当前区域摄像机。</summary>
        public CinemachineVirtualCamera ActiveCamera
            => IsValidIndex(_currentRegionIndex) ? _regionCameras[_currentRegionIndex] : null;

        /// <summary>初始化区域方向和摄像机引用。</summary>
        public void Initialize(SphereWalker walker)
        {
            Initialize(walker, FindRegionCameras());
        }

        /// <summary>使用 Director 提供的区域摄像机引用初始化本模式。</summary>
        public void Initialize(SphereWalker walker, CinemachineVirtualCamera[] regionCameras)
        {
            _walker = walker;
            CinemachineVirtualCamera[] configuredCameras = FilterValidCameras(regionCameras);
            if (configuredCameras.Length == 0)
            {
                configuredCameras = FilterValidCameras(FindRegionCameras());
            }

            _regionCameras = configuredCameras;

            int cameraCount = _regionCameras.Length;
            _regionDirections = new Vector3[cameraCount];
            for (int i = 0; i < cameraCount; i++)
            {
                CinemachineVirtualCamera regionCamera = _regionCameras[i];
                if (regionCamera == null)
                    continue;

                _regionDirections[i] = GetRegionDirection(regionCamera);
                regionCamera.Follow = null;
                regionCamera.LookAt = null;
                EnsureImpulseListener(regionCamera);
            }

            if (!HasConfiguredDirections(_regionDirections))
            {
                _regionDirections = BuildRegionDirections(cameraCount);
            }

            _calculatedCoverageRadiusDegrees = Mathf.Min(
                MaximumCoverageRadius,
                CalculateCoverageRadius(_regionDirections));

            _currentRegionIndex = -1;
            _initialized = _walker != null && cameraCount > 0;
            if (!_initialized && cameraCount == 0)
                Debug.LogWarning("[SphericalCameraRegional] 未配置区域摄像机。", this);
        }

        /// <summary>启用或停用区域摄像机选择。</summary>
        public void SetActive(bool active)
        {
            _active = active;
            if (active)
                _currentRegionIndex = -1;
        }

        /// <summary>由 Director 在 LateUpdate 中更新位置和区域选择。</summary>
        public void Tick(
            float deltaTime,
            float baseFieldOfView,
            float switchHysteresisDegrees,
            float coverageRadiusDegrees)
        {
            if (!_initialized || _walker == null)
                return;

            Vector3 center = _walker.GetEffectiveCenter();
            PositionCameras(center, baseFieldOfView, coverageRadiusDegrees);

            if (!_active)
                return;

            Vector3 playerOffset = _walker.transform.position - center;
            if (playerOffset.sqrMagnitude < MinimumDirectionSqrMagnitude)
                return;

            _currentRegionIndex = SelectRegionIndex(
                playerOffset.normalized,
                _regionDirections,
                _currentRegionIndex,
                switchHysteresisDegrees);
        }

        /// <summary>设置所有区域摄像机的运行时 FOV。</summary>
        public void SetFieldOfView(float fieldOfView)
        {
            if (!IsFinite(fieldOfView))
                return;

            for (int i = 0; i < _regionCameras.Length; i++)
            {
                if (_regionCameras[i] != null)
                    SetCameraFieldOfView(_regionCameras[i], fieldOfView);
            }
        }

        /// <summary>设置所有区域摄像机的 Priority。</summary>
        public void SetPriority(int priority)
        {
            if (_regionCameras == null)
                return;

            for (int i = 0; i < _regionCameras.Length; i++)
            {
                if (_regionCameras[i] != null)
                    _regionCameras[i].Priority = priority;
            }
        }

        /// <summary>
        /// 根据球面半径、摄像机半 FOV 和目标覆盖半径计算摄像机到球心的距离。
        /// </summary>
        public static float CalculateCameraDistance(
            float sphereRadius,
            float halfFieldOfViewDegrees,
            float coverageRadiusDegrees)
        {
            float radius = Mathf.Max(0.01f, sphereRadius);
            float halfFov = Mathf.Clamp(halfFieldOfViewDegrees, 0.01f, 89.99f);
            float coverage = Mathf.Clamp(coverageRadiusDegrees, 0f, MaximumCoverageRadius);
            float theta = halfFov * Mathf.Deg2Rad;
            float rho = coverage * Mathf.Deg2Rad;

            if (halfFov + coverage >= 90f)
            {
                return radius / Mathf.Cos(rho);
            }

            return radius * Mathf.Sin(theta + rho) / Mathf.Sin(theta);
        }

        /// <summary>
        /// 计算一组区域方向在单位球面上的最坏最近角度。
        /// <para>候选点来自球面 Voronoi 顶点及方向边界，初始化时计算一次，不进入逐帧路径。</para>
        /// </summary>
        public static float CalculateCoverageRadius(IReadOnlyList<Vector3> directions)
        {
            List<Vector3> normalizedDirections = NormalizeValidDirections(directions);
            if (normalizedDirections.Count == 0)
            {
                return MaximumCoverageRadius;
            }

            List<Vector3> candidates = new(normalizedDirections.Count * 8);
            for (int i = 0; i < normalizedDirections.Count; i++)
            {
                AddCandidate(candidates, normalizedDirections[i]);
            }

            for (int first = 0; first < normalizedDirections.Count - 1; first++)
            {
                for (int second = first + 1; second < normalizedDirections.Count; second++)
                {
                    Vector3 firstDirection = normalizedDirections[first];
                    Vector3 secondDirection = normalizedDirections[second];
                    AddCandidate(candidates, firstDirection + secondDirection);
                    AddCandidate(candidates, firstDirection - secondDirection);
                    AddCandidate(candidates, Vector3.Cross(firstDirection, secondDirection));
                }
            }

            for (int first = 0; first < normalizedDirections.Count - 2; first++)
            {
                for (int second = first + 1; second < normalizedDirections.Count - 1; second++)
                {
                    for (int third = second + 1; third < normalizedDirections.Count; third++)
                    {
                        Vector3 firstPlane = normalizedDirections[first] - normalizedDirections[second];
                        Vector3 secondPlane = normalizedDirections[first] - normalizedDirections[third];
                        AddCandidate(candidates, Vector3.Cross(firstPlane, secondPlane));
                    }
                }
            }

            float worstNearestDot = 1f;
            for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
            {
                Vector3 candidate = candidates[candidateIndex];
                float nearestDot = -1f;
                for (int directionIndex = 0; directionIndex < normalizedDirections.Count; directionIndex++)
                {
                    nearestDot = Mathf.Max(
                        nearestDot,
                        Vector3.Dot(candidate, normalizedDirections[directionIndex]));
                }

                worstNearestDot = Mathf.Min(worstNearestDot, nearestDot);
            }

            return Mathf.Acos(Mathf.Clamp(worstNearestDot, -1f, 1f)) * Mathf.Rad2Deg;
        }

        /// <summary>为固定数量的区域槽位生成近似均匀的球面方向。</summary>
        public static Vector3[] BuildRegionDirections(int count)
        {
            if (count <= 0)
            {
                return Array.Empty<Vector3>();
            }

            if (count == 4)
            {
                return NormalizeDirections(new[]
                {
                    new Vector3(1f, 1f, 1f),
                    new Vector3(-1f, -1f, 1f),
                    new Vector3(-1f, 1f, -1f),
                    new Vector3(1f, -1f, -1f)
                });
            }

            if (count == 6)
            {
                return new[]
                {
                    Vector3.right,
                    Vector3.left,
                    Vector3.up,
                    Vector3.down,
                    Vector3.forward,
                    Vector3.back
                };
            }

            if (count == 8)
            {
                return NormalizeDirections(new[]
                {
                    new Vector3(1f, 1f, 1f),
                    new Vector3(-1f, 1f, 1f),
                    new Vector3(1f, -1f, 1f),
                    new Vector3(-1f, -1f, 1f),
                    new Vector3(1f, 1f, -1f),
                    new Vector3(-1f, 1f, -1f),
                    new Vector3(1f, -1f, -1f),
                    new Vector3(-1f, -1f, -1f)
                });
            }

            var directions = new Vector3[count];
            float goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (int index = 0; index < count; index++)
            {
                float y = 1f - (2f * (index + 0.5f) / count);
                float ringRadius = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                float angle = goldenAngle * index;
                directions[index] = new Vector3(
                    Mathf.Cos(angle) * ringRadius,
                    y,
                    Mathf.Sin(angle) * ringRadius);
            }

            return directions;
        }

        /// <summary>
        /// 使用球面法线选择最近区域。currentIndex 有效时会应用角度滞回。
        /// </summary>
        public static int SelectRegionIndex(
            Vector3 playerNormal,
            Vector3[] regionDirections,
            int currentIndex,
            float hysteresisDegrees)
        {
            if (playerNormal.sqrMagnitude < MinimumDirectionSqrMagnitude ||
                regionDirections == null || regionDirections.Length == 0)
                return -1;

            playerNormal.Normalize();
            int bestIndex = -1;
            float bestScore = float.NegativeInfinity;
            for (int i = 0; i < regionDirections.Length; i++)
            {
                Vector3 direction = regionDirections[i];
                if (direction.sqrMagnitude < MinimumDirectionSqrMagnitude)
                    continue;

                float score = Vector3.Dot(playerNormal, direction.normalized);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0 || !IsValidIndex(currentIndex, regionDirections.Length) ||
                bestIndex == currentIndex)
                return bestIndex;

            Vector3 currentDirection = regionDirections[currentIndex];
            if (currentDirection.sqrMagnitude < MinimumDirectionSqrMagnitude)
                return bestIndex;

            float currentAngle = Vector3.Angle(playerNormal, currentDirection.normalized);
            float candidateAngle = Vector3.Angle(playerNormal, regionDirections[bestIndex].normalized);
            return currentAngle - candidateAngle >= Mathf.Max(0f, hysteresisDegrees)
                ? bestIndex
                : currentIndex;
        }

        private CinemachineVirtualCamera[] FindRegionCameras()
        {
            CinemachineVirtualCamera[] candidates =
                GetComponentsInChildren<CinemachineVirtualCamera>(true);
            List<CinemachineVirtualCamera> regionCameras = new(candidates.Length);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i].GetComponent<SphericalCameraFollow>() == null)
                    regionCameras.Add(candidates[i]);
            }

            return regionCameras.ToArray();
        }

        private static CinemachineVirtualCamera[] FilterValidCameras(
            CinemachineVirtualCamera[] cameras)
        {
            if (cameras == null || cameras.Length == 0)
            {
                return Array.Empty<CinemachineVirtualCamera>();
            }

            List<CinemachineVirtualCamera> validCameras = new(cameras.Length);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] != null &&
                    cameras[i].GetComponent<SphericalCameraFollow>() == null)
                {
                    validCameras.Add(cameras[i]);
                }
            }

            return validCameras.ToArray();
        }

        private Vector3 GetRegionDirection(CinemachineVirtualCamera regionCamera)
        {
            Vector3 center = _walker != null ? _walker.GetEffectiveCenter() : Vector3.zero;
            Vector3 offset = regionCamera.transform.position - center;
            if (offset.sqrMagnitude >= MinimumDirectionSqrMagnitude)
                return offset.normalized;

            Vector3 forward = -regionCamera.transform.forward;
            return forward.sqrMagnitude >= MinimumDirectionSqrMagnitude
                ? forward.normalized
                : Vector3.forward;
        }

        private void PositionCameras(
            Vector3 center,
            float baseFieldOfView,
            float coverageRadiusDegrees)
        {
            float playerRadius = Mathf.Max(0.01f, _walker.radius);
            float effectiveCoverage = coverageRadiusDegrees > 0f
                ? coverageRadiusDegrees
                : _calculatedCoverageRadiusDegrees;
            float radius = CalculateCameraDistance(
                playerRadius,
                baseFieldOfView * 0.5f,
                effectiveCoverage);
            for (int i = 0; i < _regionCameras.Length; i++)
            {
                CinemachineVirtualCamera regionCamera = _regionCameras[i];
                if (regionCamera == null || _regionDirections[i].sqrMagnitude < MinimumDirectionSqrMagnitude)
                    continue;

                Vector3 direction = _regionDirections[i];
                Vector3 position = center + direction * radius;
                regionCamera.transform.position = position;
                SphericalCoordinates.GetTangentBasis(direction, out _, out Vector3 tangentUp);
                Vector3 lookDirection = center - position;
                if (lookDirection.sqrMagnitude > MinimumDirectionSqrMagnitude)
                    regionCamera.transform.rotation = Quaternion.LookRotation(lookDirection, tangentUp);
            }
        }

        private bool IsValidIndex(int index)
        {
            return IsValidIndex(index, _regionCameras != null ? _regionCameras.Length : 0);
        }

        private static bool IsValidIndex(int index, int length)
        {
            return index >= 0 && index < length;
        }

        private static void SetCameraFieldOfView(CinemachineVirtualCamera camera, float fieldOfView)
        {
            LensSettings lens = camera.m_Lens;
            lens.FieldOfView = fieldOfView;
            camera.m_Lens = lens;
        }

        private static void EnsureImpulseListener(CinemachineVirtualCamera camera)
        {
            if (camera.GetComponent<CinemachineImpulseListener>() == null)
                camera.gameObject.AddComponent<CinemachineImpulseListener>();
        }

        private static bool HasConfiguredDirections(Vector3[] directions)
        {
            if (directions == null || directions.Length == 0)
            {
                return false;
            }

            for (int index = 0; index < directions.Length; index++)
            {
                if (directions[index].sqrMagnitude < MinimumDirectionSqrMagnitude)
                {
                    return false;
                }
            }

            return true;
        }

        private static Vector3[] NormalizeDirections(Vector3[] directions)
        {
            for (int index = 0; index < directions.Length; index++)
            {
                directions[index] = directions[index].normalized;
            }

            return directions;
        }

        private static List<Vector3> NormalizeValidDirections(IReadOnlyList<Vector3> directions)
        {
            List<Vector3> normalized = new();
            if (directions == null)
            {
                return normalized;
            }

            for (int index = 0; index < directions.Count; index++)
            {
                if (directions[index].sqrMagnitude >= MinimumCoverageCandidateSqrMagnitude)
                {
                    normalized.Add(directions[index].normalized);
                }
            }

            return normalized;
        }

        private static void AddCandidate(List<Vector3> candidates, Vector3 candidate)
        {
            if (candidate.sqrMagnitude < MinimumCoverageCandidateSqrMagnitude)
            {
                return;
            }

            Vector3 normalized = candidate.normalized;
            candidates.Add(normalized);
            candidates.Add(-normalized);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
