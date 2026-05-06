using SphereMovement.Interfaces;
using UnityEngine;

namespace SphereMovement.Core
{
    /// <summary>
    /// 朝向控制器实现
    /// </summary>
    public class OrientationController : IOrientationController
    {
        private Vector3 _lastRightDirection;

        /// <inheritdoc/>
        public Vector3 SphereCenter { get; set; } = Vector3.zero;

        /// <summary>
        /// 极点附近检测阈值（弧度）
        /// </summary>
        public float PoleThreshold { get; set; } = 1.4f; // 约80度

        /// <summary>
        /// 极点过渡平滑速度
        /// </summary>
        public float PoleTransitionSpeed { get; set; } = 10f;

        /// <inheritdoc/>
        public void UpdateOrientation(Transform target, Vector3 normalizedPos, Vector2 currentCoords)
        {
            Vector3 toCenter = (SphereCenter - target.position).normalized;
            Vector3 rightDir = GetLatitudeTangent(normalizedPos);

            float latitudeAbs = Mathf.Abs(currentCoords.y);
            bool nearPole = latitudeAbs > PoleThreshold;

            if (nearPole)
            {
                rightDir = Vector3.Lerp(rightDir, _lastRightDirection, Time.deltaTime * PoleTransitionSpeed);
                rightDir = rightDir.normalized;
            }

            Vector3 upDir = toCenter;
            Vector3 forwardDir = Vector3.Cross(rightDir, upDir).normalized;

            if (forwardDir.sqrMagnitude < 0.01f)
            {
                forwardDir = Vector3.Cross(Vector3.up, upDir).normalized;
                if (forwardDir.sqrMagnitude < 0.01f)
                {
                    forwardDir = Vector3.forward;
                }
            }

            _lastRightDirection = rightDir;

            Quaternion targetRotation = Quaternion.LookRotation(forwardDir, upDir);
            target.rotation = targetRotation;
        }

        /// <inheritdoc/>
        public Vector3 GetLatitudeTangent(Vector3 normalizedPos)
        {
            Vector3 north = Vector3.up;
            Vector3 tangent = Vector3.Cross(north, normalizedPos).normalized;

            if (tangent.sqrMagnitude < 0.01f)
            {
                tangent = Vector3.Cross(normalizedPos, Vector3.forward).normalized;
                if (tangent.sqrMagnitude < 0.01f)
                {
                    tangent = Vector3.Cross(normalizedPos, Vector3.right).normalized;
                }
            }

            return tangent;
        }

        /// <inheritdoc/>
        public Vector3 GetLongitudeTangent(Vector3 normalizedPos)
        {
            Vector3 east = Vector3.Cross(Vector3.up, normalizedPos).normalized;
            return Vector3.Cross(normalizedPos, east).normalized;
        }
    }
}
