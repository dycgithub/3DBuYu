namespace CombatSystem
{
    /// <summary>保持当前方向的普通飞行轨迹。</summary>
    public class ProjectileTrajectoryConfig : TrajectoryConfig
    {
        public override bool IsHitscan => false;

        public override UnityEngine.Vector3 GetDirection(
            ProjectileRuntime projectile,
            UnityEngine.Vector3 currentDirection)
        {
            return currentDirection;
        }
    }
}
