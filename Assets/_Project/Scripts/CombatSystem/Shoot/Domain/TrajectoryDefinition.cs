using UnityEngine;

namespace CombatSystem
{
    public abstract class TrajectoryDefinition : ScriptableObject
    {
        public abstract bool IsHitscan { get; }

        public virtual Vector3 GetDirection(ProjectileRuntime projectile, Vector3 currentDirection)
        {
            return currentDirection;
        }
    }

    [CreateAssetMenu(menuName = "Combat/Shoot Trajectory/Projectile")]
    public sealed class ProjectileTrajectoryDefinition : TrajectoryDefinition
    {
        public override bool IsHitscan => false;
    }

    [CreateAssetMenu(menuName = "Combat/Shoot Trajectory/Hitscan")]
    public sealed class HitscanTrajectoryDefinition : TrajectoryDefinition
    {
        public override bool IsHitscan => true;
    }
}
