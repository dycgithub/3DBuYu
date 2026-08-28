using Interfaces;
using Services;
using UnityEngine;

namespace CombatSystem
{
    public sealed class TrajectoryPredictor : ITrajectorySimulationService
    {
        public void Simulate(
            BulletProfile profile,
            Vector3 startPos,
            Vector3 startDir,
            IDamageable target,
            Vector3[] points,
            float timeStep)
        {
            if (profile == null || points == null || points.Length == 0)
                return;

            Vector3 position = startPos;
            Vector3 direction = startDir.sqrMagnitude > 0.0001f ? startDir.normalized : Vector3.forward;
            float speed = profile.Speed > 0f ? profile.Speed : 15f;
            float maximumDistance = profile.MaxDistance > 0f ? profile.MaxDistance : 50f;
            float distancePerStep = speed * Mathf.Max(0f, timeStep);
            float traveledDistance = 0f;

            for (int index = 0; index < points.Length; index++)
            {
                points[index] = position;
                if (traveledDistance >= maximumDistance || distancePerStep <= 0f)
                {
                    FillRemaining(points, index + 1, position);
                    return;
                }

                float stepDistance = Mathf.Min(distancePerStep, maximumDistance - traveledDistance);
                position += direction * stepDistance;
                traveledDistance += stepDistance;
            }
        }

        private static void FillRemaining(Vector3[] points, int startIndex, Vector3 position)
        {
            for (int index = startIndex; index < points.Length; index++)
                points[index] = position;
        }
    }
}
