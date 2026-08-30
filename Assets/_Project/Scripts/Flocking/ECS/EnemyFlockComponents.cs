using Unity.Entities;
using Unity.Mathematics;

namespace FlockingSystem.ECS
{
    internal static class EnemyFlockLimits
    {
        public const int MaximumAgents = 2048;
        public const int MaximumGridCells = 512;
        public const int MaximumNeighbourCandidates = 64;
    }

    /// <summary>
    /// 单个敌人的群游配置和运行时速度状态。
    /// 该组件不限制 Chunk 的实体数量；Chunk 容量由 ECS 根据实际组件大小决定。
    /// </summary>
    public struct EnemyFlockAgent : IComponentData
    {
        public float NeighbourDistance;
        public float SeparationDistance;
        public float RotationSpeed;
        public float Speed;
        public float3 Velocity;
        public float SpeedMultiplier;
        public float MinSpeed;
        public float MaxSpeed;
        public int VisualIndex;
    }

    public struct EnemyFlockNextPose : IComponentData
    {
        public float3 Position;
        public quaternion Rotation;
    }

    public struct EnemyFlockBridgeIndex : IComponentData
    {
        public int Value;
    }

    public struct EnemyFlockActive : IComponentData, IEnableableComponent
    {
    }

    /// <summary>
    /// 每个 ECS Chunk 独立持有的群游目标和随机状态。
    /// </summary>
    public struct EnemyFlockChunkGoal : IComponentData
    {
        public float3 GoalPosition;
        public uint RandomState;
        public int SeedEntityIndex;
    }

    public struct EnemyFlockWorldConfig : IComponentData
    {
        public float3 SwimCenter;
        public float3 SwimLimits;
        public float3 GridOrigin;
        public float GridCellSize;
        public int3 GridDimensions;
        public int GridCellCount;
        public float GoalChangeChance;
        public uint RandomSeed;
        public int AgentCapacity;
        public int MaximumNeighbourCandidates;
        public float MaxDeltaTime;
        public float CohesionWeight;
        public float AlignmentWeight;
        public float GoalWeight;
        public float SeparationWeight;
        public float MaxAcceleration;
        public float BoundaryWeight;
        public float BoundaryMargin;
        public float OutsideBoundsRotationMultiplier;
    }

    public readonly struct EnemyFlockProfile
    {
        public readonly int VisualIndex;
        public readonly float NeighbourDistance;
        public readonly float SeparationDistance;
        public readonly float RotationSpeed;
        public readonly float MinSpeed;
        public readonly float MaxSpeed;
        public readonly float SpeedMultiplier;

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

        public EnemyFlockAgent ToAgent(float speed)
        {
            float minSpeed = math.max(0f, MinSpeed);
            float maxSpeed = math.max(minSpeed, MaxSpeed);

            return new EnemyFlockAgent
            {
                VisualIndex = VisualIndex,
                NeighbourDistance = math.max(0.01f, NeighbourDistance),
                SeparationDistance = math.max(0.01f, SeparationDistance),
                RotationSpeed = math.max(0f, RotationSpeed),
                Speed = math.clamp(speed, minSpeed, maxSpeed),
                Velocity = new float3(0f, 0f, math.clamp(speed, minSpeed, maxSpeed)),
                SpeedMultiplier = math.max(0f, SpeedMultiplier),
                MinSpeed = minSpeed,
                MaxSpeed = maxSpeed,
            };
        }
    }

    internal struct EnemyFlockPose
    {
        public float3 Position;
        public quaternion Rotation;
    }
}
