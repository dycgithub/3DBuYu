using UnityEngine;

namespace CameraSystem
{
    /// <summary>
    /// 维护沿球面移动的逻辑切线基底，并避免重新对齐时水平轴翻转。
    /// </summary>
    public sealed class SphericalCameraMovementBasis
    {
        private const float MinimumDirectionSqrMagnitude = 0.000001f;

        private Vector3 _surfaceNormal;
        private Vector3 _right;
        private Vector3 _up;
        private bool _initialized;

        /// <summary>
        /// 使用指定表面法线和参考屏幕轴初始化移动基底。
        /// </summary>
        /// <param name="surfaceNormal">玩家所在球面的单位法线。</param>
        /// <param name="referenceRight">参考视图的世界右方向。</param>
        /// <param name="referenceUp">参考视图的世界上方向。</param>
        public void Reset(Vector3 surfaceNormal, Vector3 referenceRight, Vector3 referenceUp)
        {
            Vector3 normal = NormalizeOrForward(surfaceNormal);
            Vector3 right = Vector3.ProjectOnPlane(referenceRight, normal);
            if (!IsFinite(right) || right.sqrMagnitude < MinimumDirectionSqrMagnitude)
            {
                SphericalCoordinates.GetTangentBasis(normal, out right, out _);
            }
            else
            {
                right.Normalize();
            }

            if (_initialized && Vector3.Dot(right, _right) < 0f)
            {
                right = -right;
            }

            Vector3 up = Vector3.Cross(right, normal).normalized;
            Vector3 projectedReferenceUp = Vector3.ProjectOnPlane(referenceUp, normal);
            if (IsFinite(projectedReferenceUp) &&
                projectedReferenceUp.sqrMagnitude >= MinimumDirectionSqrMagnitude &&
                Vector3.Dot(up, projectedReferenceUp) < 0f)
            {
                up = -up;
            }

            _surfaceNormal = normal;
            _right = right;
            _up = up;
            _initialized = true;
        }

        /// <summary>
        /// 将输入转换为当前球面切线，并沿玩家的新法线运输基底。
        /// </summary>
        /// <param name="input">屏幕左右、上下输入。</param>
        /// <param name="surfaceNormal">玩家当前所在球面的法线。</param>
        /// <returns>长度不超过一的世界切线方向。</returns>
        public Vector3 GetDirection(Vector2 input, Vector3 surfaceNormal)
        {
            Vector3 normal = NormalizeOrForward(surfaceNormal);
            TransportTo(normal);

            Vector2 clampedInput = IsFinite(input)
                ? Vector2.ClampMagnitude(input, 1f)
                : Vector2.zero;
            return (_right * clampedInput.x + _up * clampedInput.y).normalized * clampedInput.magnitude;
        }

        private void TransportTo(Vector3 normal)
        {
            if (!_initialized)
            {
                Reset(normal, Vector3.zero, Vector3.zero);
                return;
            }

            if (Vector3.Angle(_surfaceNormal, normal) <= 0.0001f)
            {
                return;
            }

            Quaternion transport = Quaternion.FromToRotation(_surfaceNormal, normal);
            Vector3 transportedRight = Vector3.ProjectOnPlane(
                transport * _right,
                normal);
            Vector3 transportedUp = Vector3.ProjectOnPlane(
                transport * _up,
                normal);

            if (transportedRight.sqrMagnitude < MinimumDirectionSqrMagnitude ||
                transportedUp.sqrMagnitude < MinimumDirectionSqrMagnitude)
            {
                Reset(normal, _right, _up);
                return;
            }

            _surfaceNormal = normal;
            _right = transportedRight.normalized;
            _up = Vector3.ProjectOnPlane(transportedUp, normal).normalized;
        }

        private static Vector3 NormalizeOrForward(Vector3 direction)
        {
            return IsFinite(direction) &&
                direction.sqrMagnitude >= MinimumDirectionSqrMagnitude
                ? direction.normalized
                : Vector3.forward;
        }

        private static bool IsFinite(Vector2 value)
        {
            return IsFinite(value.x) && IsFinite(value.y);
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
