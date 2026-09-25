using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

namespace FlockingSystem.ECS
{
    /// <summary>无托管服务的独立 ECS 世界在采样前清理活动标记。</summary>
    [BurstCompile]
    internal struct EnemyFlockClearSnapshotJob : IJobParallelFor
    {
        /// <summary>按代理槽位记录本次快照是否有效。</summary>
        public NativeArray<byte> Active;

        /// <summary>清除上一帧的有效标记。</summary>
        public void Execute(int index) => Active[index] = 0;
    }

    /// <summary>向共享敌人快照写入群游速度；托管世界的位置由完整宿主快照提供。</summary>
    [BurstCompile]
    internal struct EnemyFlockGatherJob : IJobChunk
    {
        private const float MinimumVelocitySquared = 0.0001f;

        [ReadOnly] public ComponentTypeHandle<LocalTransform> LocalTransformHandle;
        [ReadOnly] public ComponentTypeHandle<EnemyFlockAgent> AgentHandle;
        [ReadOnly] public ComponentTypeHandle<EnemyFlockBridgeIndex> BridgeIndexHandle;

        /// <summary>共享快照中每个槽位是否有有效代理。</summary>
        [NativeDisableParallelForRestriction] public NativeArray<byte> Active;

        /// <summary>按空间查询槽位保存的代理位置。</summary>
        [NativeDisableParallelForRestriction] public NativeArray<float3> Positions;

        /// <summary>按空间查询槽位保存的代理速度。</summary>
        [NativeDisableParallelForRestriction] public NativeArray<float3> Velocities;

        /// <summary>为 true 时位置和有效标记来自托管空间服务。</summary>
        public bool UseSpatialSlots;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex,
            bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var transforms = chunk.GetNativeArray(ref LocalTransformHandle);
            var agents = chunk.GetNativeArray(ref AgentHandle);
            var indices = chunk.GetNativeArray(ref BridgeIndexHandle);
            var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            while (enumerator.NextEntityIndex(out int index))
            {
                int slot = UseSpatialSlots ? indices[index].SpatialSlot : indices[index].FlockSlot;
                if ((uint)slot >= (uint)Active.Length)
                {
                    continue;
                }
                if (!UseSpatialSlots)
                {
                    Active[slot] = 1;
                    Positions[slot] = transforms[index].Position;
                }
                EnemyFlockAgent agent = agents[index];
                Velocities[slot] = math.lengthsq(agent.Velocity) > MinimumVelocitySquared
                    ? agent.Velocity
                    : math.mul(
                        transforms[index].Rotation,
                        new float3(0f, 0f, math.clamp(agent.Speed, agent.MinSpeed, agent.MaxSpeed)));
            }
        }
    }
}
