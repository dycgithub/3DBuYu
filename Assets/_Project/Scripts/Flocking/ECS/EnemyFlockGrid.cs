using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace FlockingSystem.ECS
{
    /// <summary>
    /// 将世界坐标映射到固定三维网格。
    /// 网格只负责缩小邻居候选范围，最终距离判断由 Boids Job 完成。
    /// </summary>
    internal static class EnemyFlockGridMath
    {
        public static int3 GetCell(float3 position, in EnemyFlockWorldConfig config)
        {
            float cellSize = math.max(config.GridCellSize, 0.0001f);
            int3 dimensions = math.max(config.GridDimensions, new int3(1));
            int3 cell = (int3)math.floor((position - config.GridOrigin) / cellSize);
            int3 maxCell = dimensions - new int3(1);
            return math.clamp(cell, int3.zero, maxCell);
        }

        public static int GetCellIndex(float3 position, in EnemyFlockWorldConfig config)
        {
            int3 cell = GetCell(position, config);
            return cell.x + config.GridDimensions.x
                * (cell.y + config.GridDimensions.y * cell.z);
        }
    }

    /// <summary>
    /// 清理上一帧的 slot 快照。
    /// </summary>
    [BurstCompile]
    internal struct EnemyFlockClearSnapshotJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction]
        public NativeArray<byte> Active;

        public void Execute(int index)
        {
            Active[index] = 0;
        }
    }

    /// <summary>
    /// 从 ECS Chunk 采集当前帧的启用实体快照。
    /// 快照按 Bridge slot 索引，供后续并行读取。
    /// </summary>
    [BurstCompile]
    internal struct EnemyFlockGatherJob : IJobChunk
    {
        [ReadOnly] public ComponentTypeHandle<LocalTransform> LocalTransformHandle;
        [ReadOnly] public ComponentTypeHandle<EnemyFlockAgent> AgentHandle;
        [ReadOnly] public ComponentTypeHandle<EnemyFlockBridgeIndex> BridgeIndexHandle;

        [NativeDisableParallelForRestriction]
        public NativeArray<byte> Active;

        [NativeDisableParallelForRestriction]
        public NativeArray<float3> Positions;

        [NativeDisableParallelForRestriction]
        public NativeArray<float3> Velocities;

        public int AgentCapacity;

        public void Execute(
            in ArchetypeChunk chunk,
            int unfilteredChunkIndex,
            bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            var transforms = chunk.GetNativeArray(ref LocalTransformHandle);
            var agents = chunk.GetNativeArray(ref AgentHandle);
            var bridgeIndices = chunk.GetNativeArray(ref BridgeIndexHandle);
            var enumerator = new ChunkEntityEnumerator(
                useEnabledMask,
                chunkEnabledMask,
                chunk.Count);

            while (enumerator.NextEntityIndex(out int entityIndex))
            {
                int slot = bridgeIndices[entityIndex].Value;
                if ((uint)slot >= (uint)AgentCapacity)
                    continue;

                EnemyFlockAgent agent = agents[entityIndex];
                float3 velocity = agent.Velocity;
                if (math.lengthsq(velocity) <= 0.0001f)
                {
                    float speed = math.clamp(agent.Speed, agent.MinSpeed, agent.MaxSpeed);
                    velocity = math.mul(
                        transforms[entityIndex].Rotation,
                        new float3(0f, 0f, speed));
                }

                Active[slot] = 1;
                Positions[slot] = transforms[entityIndex].Position;
                Velocities[slot] = velocity;
            }
        }
    }

    /// <summary>
    /// 将启用实体按网格单元写入连续 slot 区间。
    /// CellStarts[cell] 到 CellStarts[cell + 1] 是该单元的候选范围。
    /// </summary>
    [BurstCompile]
    internal struct EnemyFlockBuildGridJob : IJob
    {
        [ReadOnly] public NativeArray<byte> Active;
        [ReadOnly] public NativeArray<float3> Positions;
        public NativeArray<int> CellCounts;
        public NativeArray<int> CellStarts;
        public NativeArray<int> CellWriteHeads;
        public NativeArray<int> SortedSlots;
        public EnemyFlockWorldConfig Config;
        public int AgentCapacity;

        public void Execute()
        {
            int cellCount = math.min(Config.GridCellCount, CellCounts.Length);
            int agentCapacity = math.min(AgentCapacity, Active.Length);

            for (int cell = 0; cell < cellCount; cell++)
                CellCounts[cell] = 0;

            for (int slot = 0; slot < agentCapacity; slot++)
            {
                if (Active[slot] == 0)
                    continue;

                int cell = EnemyFlockGridMath.GetCellIndex(Positions[slot], Config);
                if ((uint)cell < (uint)cellCount)
                    CellCounts[cell]++;
            }

            int cursor = 0;
            CellStarts[0] = 0;
            for (int cell = 0; cell < cellCount; cell++)
            {
                CellStarts[cell] = cursor;
                cursor += CellCounts[cell];
                CellWriteHeads[cell] = CellStarts[cell];
            }

            CellStarts[cellCount] = cursor;

            for (int slot = 0; slot < agentCapacity; slot++)
            {
                if (Active[slot] == 0)
                    continue;

                int cell = EnemyFlockGridMath.GetCellIndex(Positions[slot], Config);
                if ((uint)cell < (uint)cellCount)
                    SortedSlots[CellWriteHeads[cell]++] = slot;
            }
        }
    }
}
