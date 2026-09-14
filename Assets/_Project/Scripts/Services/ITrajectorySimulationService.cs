using UnityEngine;
using Interfaces;
using CombatSystem;

namespace Services
{
    /// <summary>为投射物显示组件生成离散轨迹采样点。</summary>
    public interface IProjectileTrajectorySimulationService
    {
        /// <summary>根据子弹配置和发射器有效射程预测一段轨迹。</summary>
        /// <param name="projectileProfile">子弹配置。</param>
        /// <param name="maximumTravelDistance">发射器和子弹共同决定的最大距离。</param>
        /// <param name="startPosition">轨迹起点。</param>
        /// <param name="startDirection">轨迹初始方向。</param>
        /// <param name="aimTarget">当前瞄准目标。</param>
        /// <param name="samplePoints">用于接收轨迹采样点的数组。</param>
        /// <param name="timeStep">相邻采样点之间的时间步长。</param>
        void PredictTrajectory(
            ProjectileProfileSO projectileProfile,
            float maximumTravelDistance,
            Vector3 startPosition,
            Vector3 startDirection,
            IDamageable aimTarget,
            Vector3[] samplePoints,
            float timeStep);
    }
}
