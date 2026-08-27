using UnityEngine;
using Interfaces;
using CombatSystem;

namespace Services
{
    public interface ITrajectorySimulationService
    {
        void Simulate(BulletProfile profile, Vector3 startPos, Vector3 startDir,
            IDamageable target, Vector3[] points, float timeStep);
    }
}
