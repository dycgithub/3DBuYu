using Unity.Entities;
using Unity.Mathematics;

namespace FlockingSystem.ECS
{
    /// <summary>定义敌人由群游、定向行为或冻结状态驱动。</summary>
    public enum EnemyFlockMotionMode : byte
    {
        Flocking,
        Directed,
        Frozen
    }

    /// <summary>集中保存 ECS Flocking 使用的容量上限。</summary>
    internal static class EnemyFlockLimits
    {
        /// <summary>单个群游世界允许分配的最大代理数量。</summary>
        public const int MaximumAgents = 2048;

        /// <summary>单个代理一次空间查询允许检查的最大候选数量。</summary>
        public const int MaximumNeighbourCandidates = 64;
    }

    /// <summary>
    /// 单个敌人的群游配置、运动所有权和一次性运动命令。
    /// <para>该组件保存模拟输入；最终姿态由提交系统写入托管 Transform。</para>
    /// </summary>
    public struct EnemyFlockAgent : IComponentData
    {
        /// <summary>产生对齐和分离的阈值</summary>
        public float NeighbourDistance;

        /// <summary>产生分离的阈值</summary>
        public float SeparationDistance;

        /// <summary>每秒最大旋转角度，单位为度。</summary>
        public float RotationSpeed;

        /// <summary>当前速度长度，单位为世界单位/秒。</summary>
        public float Speed;

        /// <summary>当前群游速度，单位为世界单位/秒。</summary>
        public float3 Velocity;

        /// <summary>外部速度倍率；零表示保留代理但不产生群游位移。</summary>
        public float SpeedMultiplier;

        /// <summary>模拟允许的最小速度，单位为世界单位/秒。</summary>
        public float MinSpeed;

        /// <summary>模拟允许的最大速度，单位为世界单位/秒。</summary>
        public float MaxSpeed;

        /// <summary>当前姿态的运动所有权模式。</summary>
        public EnemyFlockMotionMode MotionMode;

        /// <summary>待下一次群游模拟消费的外部速度冲量。</summary>
        public float3 ExternalImpulse;

        /// <summary>待下一次模拟消费的一次性世界位移。</summary>
        public float3 ExternalDisplacement;

        /// <summary>定向状态下一次模拟消费的世界位移。</summary>
        public float3 DirectedDisplacement;

        /// <summary>定向状态使用的世界旋转。</summary>
        public quaternion DirectedRotation;
    }

    /// <summary>模拟阶段写入、提交阶段消费的下一姿态。</summary>
    public struct EnemyFlockNextPose : IComponentData
    {
        /// <summary>下一姿态的世界坐标。</summary>
        public float3 Position;

        /// <summary>下一姿态的世界旋转。</summary>
        public quaternion Rotation;
    }

    /// <summary>把 ECS 代理映射到群游槽位和托管空间查询槽位。</summary>
    public struct EnemyFlockBridgeIndex : IComponentData
    {
        /// <summary>群游快照数组使用的稳定槽位。</summary>
        public int FlockSlot;

        /// <summary>托管空间查询快照使用的槽位；未注册时为 -1。</summary>
        public int SpatialSlot;
    }

    /// <summary>控制代理是否参与模拟和姿态提交的可启用标记。</summary>
    public struct EnemyFlockActive : IComponentData, IEnableableComponent
    {
    }

    /// <summary>每个 ECS Chunk 独立持有的群游目标和随机状态。</summary>
    public struct EnemyFlockChunkGoal : IComponentData
    {
        /// <summary>该 Chunk 内所有代理共享的目标点。</summary>
        public float3 GoalPosition;

        /// <summary>Unity.Mathematics.Random 的下一状态。</summary>
        public uint RandomState;

        /// <summary>初始化随机状态时使用的 Chunk 首实体索引。</summary>
        public int SeedEntityIndex;
    }

    /// <summary>由托管配置转换而来的 Burst 模拟参数。</summary>
    public struct EnemyFlockWorldConfig : IComponentData
    {
        /// <summary>游动区域中心的世界坐标。</summary>
        public float3 SwimCenter;

        /// <summary>相对中心的半尺寸，单位为世界单位。</summary>
        public float3 SwimLimits;

        /// <summary>空间网格原点的世界坐标。</summary>
        public float3 GridOrigin;

        /// <summary>空间网格单元边长，单位为世界单位。</summary>
        public float GridCellSize;

        /// <summary>空间网格在三个轴上的单元数量。</summary>
        public int3 GridDimensions;

        /// <summary>每个模拟步改变 Chunk 目标点的概率。</summary>
        public float GoalChangeChance;

        /// <summary>生成 Chunk 目标随机状态的世界种子。</summary>
        public uint RandomSeed;

        /// <summary>群游快照数组的逻辑容量。</summary>
        public int AgentCapacity;

        /// <summary>一次邻居查询允许检查的最大候选数量。</summary>
        public int MaximumNeighbourCandidates;

        /// <summary>单帧模拟步长上限，单位为秒。</summary>
        public float MaxDeltaTime;

        /// <summary>聚拢行为权重。</summary>
        public float CohesionWeight;

        /// <summary>对齐行为权重。</summary>
        public float AlignmentWeight;

        /// <summary>Chunk 目标点行为权重。</summary>
        public float GoalWeight;

        /// <summary>分离行为权重。</summary>
        public float SeparationWeight;

        /// <summary>单个模拟步允许施加的最大加速度。</summary>
        public float MaxAcceleration;

        /// <summary>边界回推行为权重。</summary>
        public float BoundaryWeight;

        /// <summary>开始施加边界力的预警距离。</summary>
        public float BoundaryMargin;

        /// <summary>代理越过游动区域后使用的旋转速度倍率。</summary>
        public float OutsideBoundsRotationMultiplier;
    }

    /// <summary>敌人类型对应的不可变群游配置，供服务转换为 ECS 组件。</summary>
    public readonly struct EnemyFlockProfile
    {
        /// <summary>对应 ECS 网格和材质的视觉编号。</summary>
        public readonly int VisualIndex;

        /// <summary>聚拢和对齐的邻居距离。</summary>
        public readonly float NeighbourDistance;

        /// <summary>分离力的作用距离。</summary>
        public readonly float SeparationDistance;

        /// <summary>代理每秒最大旋转角度。</summary>
        public readonly float RotationSpeed;

        /// <summary>代理最小速度。</summary>
        public readonly float MinSpeed;

        /// <summary>代理最大速度。</summary>
        public readonly float MaxSpeed;

        /// <summary>相对基础速度的运行时倍率。</summary>
        public readonly float SpeedMultiplier;

        /// <summary>保存一份尚未归一化的 Inspector 群游配置。</summary>
        public EnemyFlockProfile(
            int visualIndex,
            float neighbourDistance,
            float separationDistance,
            float rotationSpeed,
            float minSpeed,
            float maxSpeed,
            float speedMultiplier)
        {
            VisualIndex = visualIndex;
            NeighbourDistance = neighbourDistance;
            SeparationDistance = separationDistance;
            RotationSpeed = rotationSpeed;
            MinSpeed = minSpeed;
            MaxSpeed = maxSpeed;
            SpeedMultiplier = speedMultiplier;
        }

        /// <summary>
        /// 将群游配置转换为 ECS 实体组件数据，并在边界内归一化数值。
        /// </summary>
        /// <param name="speed">实体初始速度，单位为世界单位/秒。</param>
        /// <returns>可直接写入 ECS 实体的代理数据。</returns>
        public EnemyFlockAgent CreateAgentData(float speed)
        {
            float minSpeed = math.max(0f, MinSpeed);
            float maxSpeed = math.max(minSpeed, MaxSpeed);
            float initialSpeed = math.clamp(speed, minSpeed, maxSpeed);

            return new EnemyFlockAgent
            {
                NeighbourDistance = math.max(0.01f, NeighbourDistance),
                SeparationDistance = math.max(0.01f, SeparationDistance),
                RotationSpeed = math.max(0f, RotationSpeed),
                Speed = initialSpeed,
                Velocity = new float3(0f, 0f, initialSpeed),
                SpeedMultiplier = math.max(0f, SpeedMultiplier),
                MinSpeed = minSpeed,
                MaxSpeed = maxSpeed,
            };
        }
    }

    /// <summary>按群游槽位保存的位置和旋转，供托管 Transform 提交。</summary>
    internal struct EnemyFlockPose
    {
        /// <summary>代理世界坐标。</summary>
        public float3 Position;

        /// <summary>代理世界旋转。</summary>
        public quaternion Rotation;
    }
}
