using UnityEngine;
using Interfaces;
using Services;

namespace ShootingSystem.Bullets
{/// <summary>
 /// 轨迹预测
 /// </summary>
    public class TrajectoryPredictor : ITrajectorySimulationService
    {
        public Vector3[] Simulate(BulletProfile profile, Vector3 startPos, Vector3 startDir,
            IDamageable target, int steps, float timeStep)
        {
            if (profile == null || steps <= 0) return null;

            var points = new Vector3[steps];
            Vector3 pos = startPos;
            Vector3 dir = startDir.normalized;
            float speed = profile.Speed > 0f ? profile.Speed : 15f;
            float maxDist = profile.MaxDistance > 0f ? profile.MaxDistance : 50f;
            float traveled = 0f;

            for (int i = 0; i < steps; i++)
            {
                points[i] = pos;
                pos += dir * speed * timeStep;
                traveled += speed * timeStep;
                if (traveled >= maxDist) break;
            }

            return points;
        }
    }
}
