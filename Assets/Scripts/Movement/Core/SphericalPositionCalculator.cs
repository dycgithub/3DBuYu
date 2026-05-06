using SphereMovement.Interfaces;
using UnityEngine;
using SphereMovement;

namespace SphereMovement.Core
{
    /// <summary>
    /// 球面位置计算器实现
    /// </summary>
    public class SphericalPositionCalculator : ISphericalPositionCalculator
    {
        /// <inheritdoc/>
        public Vector3 SphereCenter { get; set; } = Vector3.zero;

        /// <inheritdoc/>
        public float SphereRadius { get; set; } = 5f;

        /// <inheritdoc/>
        public Vector3 CalculatePosition(Vector2 sphericalCoords)
        {
            Vector3 normalizedPos = CalculateNormalizedPosition(sphericalCoords);
            return SphereCenter + normalizedPos * SphereRadius;
        }

        /// <inheritdoc/>
        public Vector3 CalculateNormalizedPosition(Vector2 sphericalCoords)
        {
            return SphericalCoordinates.ToCartesian(sphericalCoords);
        }

        /// <inheritdoc/>
        public Vector2 CartesianToSpherical(Vector3 cartesian)
        {
            return SphericalCoordinates.FromCartesian(cartesian);
        }
    }
}
