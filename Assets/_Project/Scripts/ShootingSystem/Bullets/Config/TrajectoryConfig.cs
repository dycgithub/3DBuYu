using Unity.Entities;
using UnityEngine;

namespace ShootingSystem.Bullets.Config
{
    public abstract class TrajectoryConfig : ScriptableObject
    {
        public abstract void Initialize(EntityManager em, Entity entity, in SpawnRequest request);
    }
}
