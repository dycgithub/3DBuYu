using UnityEngine;
using Interfaces;
using ShootingSystem;

namespace Services
{
    public interface ITrajectorySimulationService
    {
        Vector3[] Simulate(BulletProfile profile, Vector3 startPos, Vector3 startDir,
            IDamageable target, int steps, float timeStep);
    }
}
