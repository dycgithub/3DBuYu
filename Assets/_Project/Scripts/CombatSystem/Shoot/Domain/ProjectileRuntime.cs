using System.Collections.Generic;
using Interfaces;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 保存投射物在飞行期间可变的运行时状态。
    /// </summary>
    public sealed class ProjectileRuntime
    {
        /// <summary>投射物数据快照。</summary>
        public ProjectileData Data { get; private set; }
        /// <summary>投射物当前位置。</summary>
        public Vector3 Position { get; set; }
        /// <summary>投射物当前移动方向。</summary>
        public Vector3 Direction { get; set; }
        /// <summary>已经旅行的距离。</summary>
        public float TraveledDistance { get; set; }
        /// <summary>距离过期剩余的时间。</summary>
        public float RemainingLifetimeSeconds { get; set; }
        /// <summary>距离完成攻击还可命中的穿透次数。</summary>
        public int RemainingPenetrationCount { get; set; }
        /// <summary>绑定的视觉实例。</summary>
        public GameObject ViewInstance { get; private set; }
        /// <summary>指示投射物是否已完成。</summary>
        public bool IsCompleted { get; set; }
        /// <summary>当前追踪的目标引用。</summary>
        public IDamageable TrackingTarget { get; set; }
        /// <summary>回旋镖是否已进入返程阶段。</summary>
        public bool IsReturning { get; set; }
        /// <summary>本次投射物是否已经执行过爆炸效果。</summary>
        public bool HasTriggeredExplosion { get; set; }
        /// <summary>回旋镖返程使用的源点。</summary>
        public Vector3 ReturnPosition { get; set; }
        /// <summary>当前模拟步长，供轨迹计算转向量使用。</summary>
        public float SimulationDeltaTime { get; set; }
        /// <summary>本次飞行已经命中的目标实例 ID。</summary>
        public readonly HashSet<int> HitTargetInstanceIds = new();

        /// <summary>创建并初始化投射物运行时状态。</summary>
        /// <param name="projectileData">投射物数据快照。</param>
        /// <param name="viewInstance">绑定的视觉实例。</param>
        public ProjectileRuntime(ProjectileData projectileData, GameObject viewInstance)
        {
            Initialize(projectileData, viewInstance);
        }

        /// <summary>使用新的投射物数据重置并初始化运行时状态。</summary>
        /// <param name="projectileData">投射物数据快照。</param>
        /// <param name="viewInstance">绑定的视觉实例。</param>
        public void Initialize(ProjectileData projectileData, GameObject viewInstance)
        {
            Data = projectileData;
            Position = projectileData.OriginPosition;
            Direction = projectileData.LaunchDirection;
            TraveledDistance = 0f;
            RemainingLifetimeSeconds = ResolveLifetimeSeconds(projectileData);
            RemainingPenetrationCount = Mathf.Max(0, projectileData.PenetrationCount);
            ViewInstance = viewInstance;
            IsCompleted = false;
            TrackingTarget = null;
            IsReturning = false;
            HasTriggeredExplosion = false;
            ReturnPosition = projectileData.OriginPosition;
            SimulationDeltaTime = 0f;
            HitTargetInstanceIds.Clear();
        }

        /// <summary>清空状态并将运行时对象标记为已完成。</summary>
        public void ResetForReuse()
        {
            Data = default;
            Position = default;
            Direction = default;
            TraveledDistance = 0f;
            RemainingLifetimeSeconds = 0f;
            RemainingPenetrationCount = 0;
            ViewInstance = null;
            IsCompleted = true;
            TrackingTarget = null;
            IsReturning = false;
            HasTriggeredExplosion = false;
            ReturnPosition = default;
            SimulationDeltaTime = 0f;
            HitTargetInstanceIds.Clear();
        }

        /// <summary>根据速度和最大距离计算投射物生命周期。</summary>
        /// <param name="data">包含速度和最大距离的投射物数据。</param>
        /// <returns>生命周期秒数；无效速度或距离使用最小兜底值。</returns>
        private static float ResolveLifetimeSeconds(ProjectileData data)
        {
            if (data.SpeedUnitsPerSecond <= 0f || data.MaximumTravelDistance <= 0f)
            {
                return 0.01f;
            }

            return data.MaximumTravelDistance / data.SpeedUnitsPerSecond;
        }
    }
}
