using Interfaces;
using Services;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>按运行时轨迹规则和 FirePoint 射程球生成预测采样点。</summary>
    public sealed class ProjectileTrajectoryPredictor : IProjectileTrajectorySimulationService
    {
        private readonly ProjectileState _predictionState = new ProjectileState(default, null);

        /// <summary>以轨迹起点同时作为射程球心，兼容通用轨迹预测接口。</summary>
        /// <param name="projectileProfile">子弹配置。</param>
        /// <param name="maximumTravelDistance">本次攻击的有效射程。</param>
        /// <param name="startPosition">轨迹起点和射程球心。</param>
        /// <param name="startDirection">轨迹初始方向。</param>
        /// <param name="aimTarget">当前瞄准目标。</param>
        /// <param name="samplePoints">要写入的世界坐标采样数组。</param>
        /// <param name="timeStep">相邻采样点之间的时间步长。</param>
        public void PredictTrajectory(
            ProjectileProfileSO projectileProfile,
            float maximumTravelDistance,
            Vector3 startPosition,
            Vector3 startDirection,
            IDamageable aimTarget,
            Vector3[] samplePoints,
            float timeStep)
        {
            PredictTrajectoryWithinRange(
                projectileProfile,
                maximumTravelDistance,
                startPosition,
                startPosition,
                startDirection,
                aimTarget,
                samplePoints,
                timeStep);
        }

        /// <summary>按轨迹配置和弹速倍率解析预测用的有效速度。</summary>
        /// <param name="projectileProfile">本次攻击使用的投射物配置。</param>
        /// <param name="maximumTravelDistance">本次攻击的有效射程。</param>
        /// <returns>投射物应用轨迹配置及速度倍率后的速度。</returns>
        internal static float ResolveEffectiveSpeed(
            ProjectileProfileSO projectileProfile,
            float maximumTravelDistance)
        {
            if (projectileProfile == null)
            {
                return 0f;
            }

            ProjectileData projectileData = new()
            {
                SpeedUnitsPerSecond = Mathf.Max(0f, projectileProfile.SpeedUnitsPerSecond),
                MaximumTravelDistance = Mathf.Max(0f, maximumTravelDistance),
                CollisionRadius = Mathf.Max(0f, projectileProfile.CollisionRadius)
            };
            projectileProfile.TrajectoryDefinition?.ConfigureData(ref projectileData);

            return Mathf.Max(0f, projectileData.SpeedUnitsPerSecond) *
                Mathf.Max(0f, projectileProfile.ProjectileSpeedMultiplier);
        }

        /// <summary>按有效速度和轨迹总路程解析采样时间，回旋镖预留返程转折步。</summary>
        /// <param name="projectileProfile">本次攻击使用的投射物配置。</param>
        /// <param name="maximumTravelDistance">本次攻击的有效射程。</param>
        /// <param name="requestedTimeStep">显示组件请求的采样时间。</param>
        /// <param name="samplePointCount">需要生成的采样点数量。</param>
        /// <returns>覆盖完整轨迹的采样时间。</returns>
        internal static float ResolveTimeStep(
            ProjectileProfileSO projectileProfile,
            float maximumTravelDistance,
            float requestedTimeStep,
            int samplePointCount)
        {
            float timeStep = float.IsNaN(requestedTimeStep) || float.IsInfinity(requestedTimeStep)
                ? 0.05f
                : Mathf.Max(0.01f, requestedTimeStep);
            float pathDistance = Mathf.Max(0f, maximumTravelDistance);
            int sampleIntervals = Mathf.Max(1, samplePointCount - 1);
            if (projectileProfile != null &&
                projectileProfile.TrajectoryDefinition is BoomerangTrajectoryDefinitionSO)
            {
                pathDistance *= 2f;
                sampleIntervals = Mathf.Max(1, sampleIntervals - 1);
            }

            float speed = ResolveEffectiveSpeed(projectileProfile, maximumTravelDistance);
            if (speed <= 0f || pathDistance <= 0f)
            {
                return timeStep;
            }

            float requestedDistance = speed * timeStep * sampleIntervals;
            return requestedDistance >= pathDistance
                ? timeStep
                : pathDistance / (speed * sampleIntervals);
        }

        /// <summary>从独立生成起点开始，按指定世界球心预测投射物轨迹。</summary>
        /// <param name="projectileProfile">子弹配置。</param>
        /// <param name="maximumTravelDistance">本次攻击的有效射程。</param>
        /// <param name="rangeOrigin">限制投射物活动范围的世界球心。</param>
        /// <param name="startPosition">最终确定的投射物生成位置。</param>
        /// <param name="startDirection">轨迹初始方向。</param>
        /// <param name="aimTarget">当前瞄准目标。</param>
        /// <param name="samplePoints">要写入的世界坐标采样数组。</param>
        /// <param name="timeStep">相邻采样点之间的时间步长。</param>
        internal void PredictTrajectoryWithinRange(
            ProjectileProfileSO projectileProfile,
            float maximumTravelDistance,
            Vector3 rangeOrigin,
            Vector3 startPosition,
            Vector3 startDirection,
            IDamageable aimTarget,
            Vector3[] samplePoints,
            float timeStep,
            ISpatialQueryService spatialQueryService = null)
        {
            if (projectileProfile == null || samplePoints == null || samplePoints.Length == 0)
            {
                return;
            }

            Vector3 direction = startDirection.sqrMagnitude > 0.0001f
                ? startDirection.normalized
                : Vector3.forward;
            maximumTravelDistance = Mathf.Max(
                0f,
                Mathf.Min(maximumTravelDistance, projectileProfile.MaximumTravelDistance));
            startPosition = ProjectileLaunchPattern.ClampToWorldRange(
                startPosition,
                rangeOrigin,
                maximumTravelDistance);

            float timeStepSeconds = float.IsNaN(timeStep) || float.IsInfinity(timeStep)
                ? 0f
                : Mathf.Max(0f, timeStep);
            TrajectoryDefinitionSO trajectory = projectileProfile.TrajectoryDefinition;
            ProjectileData data = new()
            {
                ProjectileProfile = projectileProfile,
                ProjectileProfileSnapshot = projectileProfile,
                OriginPosition = startPosition,
                RangeOriginPosition = rangeOrigin,
                HasWorldRangeOrigin = true,
                LaunchDirection = direction,
                SpeedUnitsPerSecond = Mathf.Max(0f, projectileProfile.SpeedUnitsPerSecond),
                MaximumTravelDistance = maximumTravelDistance
            };
            trajectory?.ConfigureData(ref data);
            data.SpeedUnitsPerSecond *= Mathf.Max(
                0f,
                projectileProfile.ProjectileSpeedMultiplier);
            _predictionState.Initialize(data, null);
            _predictionState.TrackingTarget = aimTarget;
            trajectory?.InitializeRuntime(_predictionState, spatialQueryService);

            for (int index = 0; index < samplePoints.Length; index++)
            {
                samplePoints[index] = _predictionState.Position;
                if (index == samplePoints.Length - 1)
                {
                    return;
                }

                _predictionState.SimulationDeltaTime = timeStepSeconds;
                direction = trajectory != null
                    ? trajectory.GetDirection(
                        _predictionState,
                        _predictionState.Direction,
                        spatialQueryService).normalized
                    : _predictionState.Direction;
                if (direction.sqrMagnitude < 0.0001f)
                {
                    direction = Vector3.forward;
                }

                _predictionState.Direction = direction;
                bool canContinueBeyondTravelLimit = trajectory != null &&
                    trajectory.CanContinueBeyondTravelLimit(_predictionState);
                float remainingDistance = Mathf.Max(
                    0f,
                    _predictionState.Data.MaximumTravelDistance - _predictionState.TraveledDistance);
                if (remainingDistance <= 0f && !canContinueBeyondTravelLimit)
                {
                    FillRemaining(samplePoints, index + 1, _predictionState.Position);
                    return;
                }

                float stepDistance = _predictionState.Data.SpeedUnitsPerSecond * timeStepSeconds;
                if (!canContinueBeyondTravelLimit)
                {
                    stepDistance = Mathf.Min(stepDistance, remainingDistance);
                }

                if (trajectory is BoomerangTrajectoryDefinitionSO && _predictionState.IsReturning)
                {
                    stepDistance = Mathf.Min(
                        stepDistance,
                        Vector3.Distance(
                            _predictionState.Position,
                            _predictionState.ReturnPosition));
                }

                stepDistance = Mathf.Min(
                    stepDistance,
                    ProjectileLaunchPattern.GetWorldRangeStepDistance(
                        _predictionState.Position,
                        direction,
                        _predictionState.RangeOriginPosition,
                        _predictionState.Data.MaximumTravelDistance));
                if (stepDistance <= 0f)
                {
                    bool rangeExpired = UpdateWorldRangeBoundary(
                        trajectory,
                        spatialQueryService);
                    _predictionState.RemainingLifetimeSeconds -= timeStepSeconds;
                    if (ShouldStopPrediction(trajectory, rangeExpired))
                    {
                        FillRemaining(samplePoints, index + 1, _predictionState.Position);
                        return;
                    }

                    continue;
                }

                _predictionState.Position += direction * stepDistance;
                _predictionState.TraveledDistance += stepDistance;
                _predictionState.RemainingLifetimeSeconds -= timeStepSeconds;
                _predictionState.Position = ProjectileLaunchPattern.ClampToWorldRange(
                    _predictionState.Position,
                    _predictionState.RangeOriginPosition,
                    _predictionState.Data.MaximumTravelDistance);
                bool reachedWorldRangeLimit = UpdateWorldRangeBoundary(
                    trajectory,
                    spatialQueryService);
                if (ShouldStopPrediction(trajectory, reachedWorldRangeLimit))
                {
                    FillRemaining(samplePoints, index + 1, _predictionState.Position);
                    return;
                }
            }
        }

        /// <summary>更新射程边界状态，并在回旋镖触边时启动返程。</summary>
        private bool UpdateWorldRangeBoundary(
            TrajectoryDefinitionSO trajectory,
            ISpatialQueryService spatialQueryService)
        {
            if (!ProjectileLaunchPattern.IsAtWorldRangeBoundary(
                    _predictionState.Position,
                    _predictionState.RangeOriginPosition,
                    _predictionState.Data.MaximumTravelDistance))
            {
                return false;
            }

            if (trajectory is BoomerangTrajectoryDefinitionSO && !_predictionState.IsReturning)
            {
                _predictionState.TraveledDistance = Mathf.Max(
                    _predictionState.TraveledDistance,
                    _predictionState.Data.MaximumTravelDistance);
                _predictionState.Direction = trajectory.GetDirection(
                    _predictionState,
                    _predictionState.Direction,
                    spatialQueryService).normalized;
            }

            return trajectory == null || !trajectory.CanContinueBeyondTravelLimit(_predictionState);
        }

        /// <summary>判断轨迹、时间或世界射程是否已经结束。</summary>
        private bool ShouldStopPrediction(
            TrajectoryDefinitionSO trajectory,
            bool worldRangeExpired)
        {
            if (trajectory != null && trajectory.ShouldComplete(_predictionState))
            {
                return true;
            }

            if (_predictionState.RemainingLifetimeSeconds <= 0f || worldRangeExpired)
            {
                return true;
            }

            return _predictionState.TraveledDistance >= _predictionState.Data.MaximumTravelDistance &&
                (trajectory == null || !trajectory.CanContinueBeyondTravelLimit(_predictionState));
        }

        /// <summary>用最后一个有效位置填充轨迹末尾的剩余采样点。</summary>
        /// <param name="points">要填充的采样点数组。</param>
        /// <param name="startIndex">开始填充的数组索引。</param>
        /// <param name="position">用于填充的世界坐标。</param>
        private static void FillRemaining(Vector3[] points, int startIndex, Vector3 position)
        {
            for (int index = startIndex; index < points.Length; index++)
            {
                points[index] = position;
            }
        }
    }
}
