using Unity.Entities;
using Unity.Mathematics;

namespace FlockingSystem.ECS
{
    /// <summary>
    /// 鱼群个体运行时数据（ECS Component）。
    /// 替代原 FlockAgent MonoBehaviour 中的运行时字段。
    /// </summary>
    public struct FlockAgentData : IComponentData
    {
        /// <summary>邻居检测半径（聚合/对齐范围）。</summary>
        public float NeighbourDistance;

        /// <summary>分离触发距离（避免碰撞）。</summary>
        public float SeparationDistance;

        /// <summary>转向平滑速度。</summary>
        public float RotationSpeed;

        /// <summary>当前速度（对齐规则会修改此值）。</summary>
        public float Speed;

        /// <summary>外部速度倍率（如冰冻效果减慢鱼群）。</summary>
        public float SpeedMultiplier;

        /// <summary>最小速度。</summary>
        public float MinSpeed;

        /// <summary>最大速度。</summary>
        public float MaxSpeed;
    }

    /// <summary>
    /// 鱼群全局目标数据（Singleton Component）。
    /// 每帧由 FlockManager 更新，ECS System 读取。
    /// </summary>
    public struct FlockGoalData : IComponentData
    {
        /// <summary>鱼群趋向目标点（世界坐标）。</summary>
        public float3 GoalPos;

        /// <summary>游泳区域中心（世界坐标）。</summary>
        public float3 SwimCenter;

        /// <summary>游泳区域半尺寸（超出后折返）。</summary>
        public float3 SwimLimits;
    }
}
