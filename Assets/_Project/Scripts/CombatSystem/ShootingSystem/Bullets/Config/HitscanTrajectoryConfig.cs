namespace CombatSystem
{
    /// <summary>使用一次 Raycast 立即结算的即时命中轨迹。</summary>
    public class HitscanTrajectoryConfig : TrajectoryConfig
    {
        public override bool IsHitscan => true;

        public override UnityEngine.Vector3 GetDirection(
            ProjectileRuntime projectile,
            UnityEngine.Vector3 currentDirection)
        {
            return currentDirection;
        }
    }
}
