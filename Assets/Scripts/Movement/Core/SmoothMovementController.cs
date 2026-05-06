using SphereMovement.Interfaces;
using UnityEngine;

namespace SphereMovement.Core
{
    /// <summary>
    /// 平滑移动控制器实现
    /// </summary>
    public class SmoothMovementController : ISmoothMovementController
    {
        private Vector2 _velocity;

        /// <inheritdoc/>
        public bool UseSmoothMovement { get; set; } = true;

        /// <inheritdoc/>
        public float SmoothTime { get; set; } = 0.1f;

        /// <inheritdoc/>
        public Vector2 SmoothMove(Vector2 current, Vector2 target)
        {
            if (!UseSmoothMovement)
            {
                return target;
            }

            Vector2 result = current;
            result.x = Mathf.SmoothDamp(current.x, target.x, ref _velocity.x, SmoothTime);
            result.y = Mathf.SmoothDamp(current.y, target.y, ref _velocity.y, SmoothTime);
            return result;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            _velocity = Vector2.zero;
        }
    }
}
