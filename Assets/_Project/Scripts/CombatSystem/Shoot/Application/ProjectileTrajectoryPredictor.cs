using Interfaces;
using Services;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 使用子弹速度和发射器有效最大距离生成直线轨迹采样点。
    /// </summary>
    public sealed class ProjectileTrajectoryPredictor : IProjectileTrajectorySimulationService
    {
        /// <summary>生成轨迹采样点；当前轨迹预测不改变目标状态。</summary>
        /// <param name="projectileProfile">子弹配置。</param>
        /// <param name="maximumTravelDistance">发射器和子弹共同决定的最大距离。</param>
        /// <param name="startPosition">轨迹起点。</param>
        /// <param name="startDirection">轨迹初始方向。</param>
        /// <param name="aimTarget">当前瞄准目标，保留用于未来曲线/制导轨迹。</param>
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
            if (projectileProfile == null || samplePoints == null || samplePoints.Length == 0)
            {
                return;
            }

            Vector3 position = startPosition;
            Vector3 direction = startDirection.sqrMagnitude > 0.0001f
                ? startDirection.normalized
                : Vector3.forward;
            float speedUnitsPerSecond = Mathf.Max(0f, projectileProfile.SpeedUnitsPerSecond);
            maximumTravelDistance = Mathf.Max(
                0f,
                Mathf.Min(maximumTravelDistance, projectileProfile.MaximumTravelDistance));
            float distancePerStep = speedUnitsPerSecond * Mathf.Max(0f, timeStep);
            float traveledDistance = 0f;

            for (int index = 0; index < samplePoints.Length; index++)
            {
                samplePoints[index] = position;
                if (traveledDistance >= maximumTravelDistance || distancePerStep <= 0f)
                {
                    FillRemaining(samplePoints, index + 1, position);
                    return;
                }

                float stepDistance = Mathf.Min(
                    distancePerStep,
                    maximumTravelDistance - traveledDistance);
                position += direction * stepDistance;
                traveledDistance += stepDistance;
            }
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
