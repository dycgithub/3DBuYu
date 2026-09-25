using Interfaces;
using Services;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>飞出最大距离后折返发射源并在返程中继续命中的轨迹。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Trajectory/Boomerang")]
    public sealed class BoomerangTrajectoryDefinitionSO : TrajectoryDefinitionSO
    {
        /// <summary>返回发射源时允许的距离误差。</summary>
        [Min(0.01f)] public float ReturnDistance = 0.15f;

        /// <summary>回旋镖由投射物扫掠处理，不执行 Hitscan。</summary>
        public override bool IsHitscan => false;

        /// <summary>保存返程点，并确保返程时长不会先于归位而结束。</summary>
        public override void InitializeRuntime(
            ProjectileRuntime projectile,
            ISpatialQueryService spatialQueryService)
        {
            if (projectile == null)
            {
                return;
            }

            projectile.ReturnPosition = projectile.Data.OriginPosition;
            float speed = Mathf.Max(0.01f, projectile.Data.SpeedUnitsPerSecond);
            float returnLifetime = projectile.Data.MaximumTravelDistance / speed * 2f + 0.1f;
            projectile.RemainingLifetimeSeconds = Mathf.Max(
                projectile.RemainingLifetimeSeconds,
                returnLifetime);
        }

        /// <summary>达到最大出程距离后令投射物朝发射源返回。</summary>
        public override Vector3 GetDirection(ProjectileRuntime projectile, Vector3 currentDirection)
        {
            if (projectile == null)
            {
                return currentDirection;
            }

            if (!projectile.IsReturning &&
                projectile.TraveledDistance >= projectile.Data.MaximumTravelDistance)
            {
                projectile.IsReturning = true;
            }

            if (!projectile.IsReturning)
            {
                return currentDirection;
            }

            Vector3 returnDirection = projectile.ReturnPosition - projectile.Position;
            return returnDirection.sqrMagnitude > 0.0001f
                ? returnDirection.normalized
                : currentDirection;
        }

        /// <summary>允许返程阶段越过出程距离限制。</summary>
        public override bool CanContinueBeyondTravelLimit(ProjectileRuntime projectile)
        {
            return projectile != null && projectile.IsReturning;
        }

        /// <summary>投射物抵达发射源附近后完成轨迹。</summary>
        public override bool ShouldComplete(ProjectileRuntime projectile)
        {
            if (projectile == null)
            {
                return false;
            }

            if (!projectile.IsReturning &&
                projectile.TraveledDistance >= projectile.Data.MaximumTravelDistance)
            {
                projectile.IsReturning = true;
            }

            return projectile.IsReturning &&
                Vector3.Distance(projectile.Position, projectile.ReturnPosition) <=
                Mathf.Max(0.01f, ReturnDistance);
        }

        /// <summary>返程阶段命中后继续向发射源移动。</summary>
        public override bool ContinuesAfterHit => true;
    }
}
