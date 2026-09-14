using UnityEngine;
using Interfaces;
using Services;
using SpatialSystem.Bridge;

namespace CombatSystem
{
    /// <summary>
    /// 描述投射物移动或 Hitscan 查询的轨迹策略。
    /// </summary>
    public abstract class TrajectoryDefinitionSO : ScriptableObject
    {
        /// <summary>指示当前轨迹是否直接执行一次射线命中查询。</summary>
        public abstract bool IsHitscan { get; }

        /// <summary>根据当前运行时状态计算下一步移动方向。</summary>
        /// <param name="projectile">投射物运行时状态。</param>
        /// <param name="currentDirection">当前移动方向。</param>
        /// <returns>下一步使用的方向。</returns>
        public virtual Vector3 GetDirection(ProjectileRuntime projectile, Vector3 currentDirection)
        {
            return currentDirection;
        }

        /// <summary>按轨迹类型调整生成时的投射物数据。</summary>
        /// <param name="projectileData">待调整的投射物数据快照。</param>
        public virtual void ConfigureData(ref ProjectileData projectileData)
        {
        }

        /// <summary>初始化轨迹需要的运行时状态。</summary>
        /// <param name="projectile">待初始化的投射物状态。</param>
        /// <param name="spatialQueryService">可选的空间查询服务。</param>
        public virtual void InitializeRuntime(
            ProjectileRuntime projectile,
            ISpatialQueryService spatialQueryService)
        {
        }

        /// <summary>指示达到最大距离后轨迹是否仍可继续运行。</summary>
        /// <param name="projectile">投射物运行时状态。</param>
        /// <returns>轨迹仍有后续阶段时返回 true。</returns>
        public virtual bool CanContinueBeyondTravelLimit(ProjectileRuntime projectile)
        {
            return false;
        }

        /// <summary>判断轨迹是否已完成自身的生命周期。</summary>
        /// <param name="projectile">投射物运行时状态。</param>
        /// <returns>轨迹完成时返回 true。</returns>
        public virtual bool ShouldComplete(ProjectileRuntime projectile)
        {
            return false;
        }

        /// <summary>指示命中后是否继续查询后续目标。</summary>
        public virtual bool ContinuesAfterHit => false;
    }

    /// <summary>描述沿当前方向移动的普通投射物轨迹。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Trajectory/Projectile")]
    public sealed class ProjectileTrajectoryDefinitionSO : TrajectoryDefinitionSO
    {
        /// <summary>普通投射物不是 Hitscan。</summary>
        public override bool IsHitscan => false;
    }

    /// <summary>描述会逐步转向当前目标的追踪轨迹。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Trajectory/Homing")]
    public sealed class HomingTrajectoryDefinitionSO : TrajectoryDefinitionSO
    {
        /// <summary>每秒允许的最大转向弧度。</summary>
        [Min(0f)] public float TurnRateRadiansPerSecond = 8f;

        /// <summary>没有预设目标时的最大搜索半径。</summary>
        [Min(0f)] public float TargetSearchRadius = 50f;

        /// <summary>追踪轨迹不是 Hitscan。</summary>
        public override bool IsHitscan => false;

        /// <summary>生成时通过空间服务寻找一个最近的有效目标。</summary>
        public override void InitializeRuntime(
            ProjectileRuntime projectile,
            ISpatialQueryService spatialQueryService)
        {
            if (projectile == null || projectile.TrackingTarget != null || spatialQueryService == null)
            {
                return;
            }

            int layerMask = projectile.Data.TargetMode == ProjectileTargetMode.Player
                ? SpatialRegistry.LAYER_PLAYER
                : SpatialRegistry.LAYER_ENEMY;
            projectile.TrackingTarget = spatialQueryService.QueryNearest(
                projectile.Position,
                Mathf.Max(0f, TargetSearchRadius),
                layerMask);
        }

        /// <summary>将当前方向以有限角速度转向追踪目标。</summary>
        public override Vector3 GetDirection(ProjectileRuntime projectile, Vector3 currentDirection)
        {
            if (projectile == null || projectile.TrackingTarget == null ||
                !projectile.TrackingTarget.IsAlive)
            {
                return currentDirection;
            }

            Vector3 targetDirection = projectile.TrackingTarget.Position - projectile.Position;
            if (targetDirection.sqrMagnitude <= 0.0001f)
            {
                return currentDirection;
            }

            Vector3 direction = currentDirection.sqrMagnitude > 0.0001f
                ? currentDirection.normalized
                : Vector3.forward;
            float maxRadians = Mathf.Max(0f, TurnRateRadiansPerSecond) *
                Mathf.Max(0f, projectile.SimulationDeltaTime);
            return Vector3.RotateTowards(
                direction,
                targetDirection.normalized,
                maxRadians,
                0f).normalized;
        }
    }

    /// <summary>描述命中半径固定且生命周期较短的连续激光轨迹。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Trajectory/Laser")]
    public sealed class LaserTrajectoryDefinitionSO : TrajectoryDefinitionSO
    {
        /// <summary>激光的最小扫掠半径。</summary>
        [Min(0f)] public float Radius = 0.35f;

        /// <summary>激光投射物的最大连续生命周期。</summary>
        [Min(0.01f)] public float LifetimeSeconds = 0.15f;

        /// <summary>激光不是 Hitscan。</summary>
        public override bool IsHitscan => false;

        /// <summary>将激光的半径写入投射物数据快照。</summary>
        public override void ConfigureData(ref ProjectileData projectileData)
        {
            projectileData.CollisionRadius = Mathf.Max(
                projectileData.CollisionRadius,
                Mathf.Max(0f, Radius));
        }

        /// <summary>将生命周期限制在短时间窗口内。</summary>
        public override void InitializeRuntime(
            ProjectileRuntime projectile,
            ISpatialQueryService spatialQueryService)
        {
            if (projectile == null)
            {
                return;
            }

            projectile.RemainingLifetimeSeconds = Mathf.Min(
                projectile.RemainingLifetimeSeconds,
                Mathf.Max(0.01f, LifetimeSeconds));
        }

        /// <summary>激光命中后仍继续查询其他目标。</summary>
        public override bool ContinuesAfterHit => true;
    }

    /// <summary>描述飞出最大距离后转向发射源的回旋镖轨迹。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Trajectory/Boomerang")]
    public sealed class BoomerangTrajectoryDefinitionSO : TrajectoryDefinitionSO
    {
        /// <summary>回到源点时允许的距离误差。</summary>
        [Min(0.01f)] public float ReturnDistance = 0.15f;

        /// <summary>回旋镖不是 Hitscan。</summary>
        public override bool IsHitscan => false;

        /// <summary>初始化回旋源点并给返程预留生命周期。</summary>
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

        /// <summary>达到出程距离后切换到朝源点移动。</summary>
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

        /// <summary>返程阶段允许越过出程距离限制。</summary>
        public override bool CanContinueBeyondTravelLimit(ProjectileRuntime projectile)
        {
            return projectile != null && projectile.IsReturning;
        }

        /// <summary>回到发射源后完成投射物。</summary>
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

        /// <summary>命中后继续返程，不在首个目标处结束。</summary>
        public override bool ContinuesAfterHit => true;
    }

    /// <summary>描述只执行一次射线查询的 Hitscan 轨迹。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Trajectory/Hitscan")]
    public sealed class HitscanTrajectoryDefinitionSO : TrajectoryDefinitionSO
    {
        /// <summary>Hitscan 轨迹直接执行一次射线查询。</summary>
        public override bool IsHitscan => true;
    }
}
