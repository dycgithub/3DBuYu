using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Jobs;
using SpatialIndexing;

namespace FlockingSystem.ECS
{
    /// <summary>
    /// 把模拟阶段生成的 ECS 姿态提交给 LocalTransform 和托管 Transform。
    /// <para>Transform 映射只由槽位数组维护，交换删除时不额外保存重复的 slot 列表。</para>
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnemyFlockSimulationSystem))]
    public partial class EnemyFlockCommitSystem : SystemBase
    {
        private const int InvalidSlot = -1;

        /// <summary>同时包含下一姿态和活动标记的 ECS 代理查询。</summary>
        private EntityQuery _query;

        /// <summary>由提交 Job 并行写入的托管 Transform 集合。</summary>
        private TransformAccessArray _transforms;

        /// <summary>TransformAccessArray 索引到群游槽位的映射。</summary>
        private NativeArray<int> _slotsByTransformIndex;

        /// <summary>Transform 是否接收 ECS 姿态。</summary>
        private NativeArray<byte> _activeByTransformIndex;

        /// <summary>按群游槽位保存的中间姿态。</summary>
        private NativeArray<EnemyFlockPose> _poses;

        private int _maximumAgents = EnemyFlockLimits.MaximumAgents;
        private ComponentTypeHandle<LocalTransform> _localTransformHandle;
        private ComponentTypeHandle<EnemyFlockNextPose> _nextPoseHandle;
        private ComponentTypeHandle<EnemyFlockBridgeIndex> _bridgeIndexHandle;

        protected override void OnCreate()
        {
            _query = GetEntityQuery(
                ComponentType.ReadOnly<EnemyFlockNextPose>(),
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadOnly<EnemyFlockBridgeIndex>(),
                ComponentType.ReadOnly<EnemyFlockActive>());
            _localTransformHandle = GetComponentTypeHandle<LocalTransform>(false);
            _nextPoseHandle = GetComponentTypeHandle<EnemyFlockNextPose>(true);
            _bridgeIndexHandle = GetComponentTypeHandle<EnemyFlockBridgeIndex>(true);
            _transforms = new TransformAccessArray(EnemyFlockLimits.MaximumAgents);
            _slotsByTransformIndex = new NativeArray<int>(EnemyFlockLimits.MaximumAgents, Allocator.Persistent);
            _activeByTransformIndex = new NativeArray<byte>(EnemyFlockLimits.MaximumAgents, Allocator.Persistent);
            _poses = new NativeArray<EnemyFlockPose>(EnemyFlockLimits.MaximumAgents, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            Dependency.Complete();
            if (_transforms.isCreated)
            {
                _transforms.Dispose();
            }

            if (_slotsByTransformIndex.IsCreated)
            {
                _slotsByTransformIndex.Dispose();
            }

            if (_activeByTransformIndex.IsCreated)
            {
                _activeByTransformIndex.Dispose();
            }

            if (_poses.IsCreated)
            {
                _poses.Dispose();
            }
        }

        /// <summary>
        /// 注册一个托管敌人的 Transform，并返回其提交数组索引。
        /// </summary>
        /// <param name="target">需要同步姿态的托管 Transform。</param>
        /// <param name="slot">对应的 ECS 群游 slot。</param>
        /// <returns>注册成功后的数组索引；容量不足或目标无效时返回 -1。</returns>
        public int RegisterAgentTransform(Transform target, int slot)
        {
            Dependency.Complete();
            if (!target || _transforms.length >= _maximumAgents)
            {
                return InvalidSlot;
            }

            _transforms.Add(target);
            int transformIndex = _transforms.length - 1;
            _slotsByTransformIndex[transformIndex] = slot;
            _activeByTransformIndex[transformIndex] = 0;
            return transformIndex;
        }

        /// <summary>
        /// 设置托管 Transform 提交数组的最大代理容量。
        /// </summary>
        /// <param name="maximumAgents">要使用的最大代理数量。</param>
        public void ConfigureAgentCapacity(int maximumAgents)
        {
            Dependency.Complete();
            _maximumAgents = Mathf.Clamp(maximumAgents, 1, EnemyFlockLimits.MaximumAgents);
        }

        /// <summary>
        /// 设置指定托管 Transform 是否接收 ECS 姿态提交。
        /// </summary>
        /// <param name="transformIndex">TransformAccessArray 中的索引。</param>
        /// <param name="active">是否启用姿态同步。</param>
        public void SetAgentTransformActive(int transformIndex, bool active)
        {
            Dependency.Complete();
            if (transformIndex < 0 || transformIndex >= _transforms.length)
            {
                return;
            }

            _activeByTransformIndex[transformIndex] = active ? (byte)1 : (byte)0;
        }

        /// <summary>
        /// 移除指定托管 Transform，并在交换删除时返回被移动代理的 slot。
        /// </summary>
        /// <param name="transformIndex">要移除的 Transform 数组索引。</param>
        /// <returns>被交换到移除位置的代理 slot；没有移动代理时返回 -1。</returns>
        public int UnregisterAgentTransform(int transformIndex)
        {
            Dependency.Complete();
            if (transformIndex < 0 || transformIndex >= _transforms.length)
            {
                return InvalidSlot;
            }

            int lastIndex = _transforms.length - 1;
            int movedSlot = InvalidSlot;
            if (transformIndex != lastIndex)
            {
                movedSlot = _slotsByTransformIndex[lastIndex];
                _slotsByTransformIndex[transformIndex] = movedSlot;
                _activeByTransformIndex[transformIndex] = _activeByTransformIndex[lastIndex];
            }

            _activeByTransformIndex[lastIndex] = 0;
            _transforms.RemoveAtSwapBack(transformIndex);
            return movedSlot;
        }

        /// <summary>
        /// 先写回 ECS LocalTransform 和槽位姿态，再并行同步托管 Transform。
        /// </summary>
        protected override void OnUpdate()
        {
            if (_transforms.length == 0 || _query.IsEmptyIgnoreFilter)
            {
                return;
            }

            _localTransformHandle.Update(this);
            _nextPoseHandle.Update(this);
            _bridgeIndexHandle.Update(this);

            Dependency = new EnemyFlockCommitEntitiesJob
            {
                LocalTransformHandle = _localTransformHandle,
                NextPoseHandle = _nextPoseHandle,
                BridgeIndexHandle = _bridgeIndexHandle,
                Poses = _poses,
            }.ScheduleParallel(_query, Dependency);

            Dependency = new EnemyFlockApplyTransformJob
            {
                Poses = _poses,
                SlotsByTransformIndex = _slotsByTransformIndex,
                ActiveByTransformIndex = _activeByTransformIndex,
            }.Schedule(_transforms, Dependency);

            World.GetExistingSystemManaged<SpatialIndexSystem>()?.Service?.InvalidateEnemySnapshot();
        }
    }

    /// <summary>把下一姿态写入 ECS Transform，并按群游槽位保存给托管提交 Job。</summary>
    [BurstCompile]
    internal struct EnemyFlockCommitEntitiesJob : IJobChunk
    {
        /// <summary>读写实体的 LocalTransform。</summary>
        public ComponentTypeHandle<LocalTransform> LocalTransformHandle;

        /// <summary>读取模拟阶段生成的下一姿态。</summary>
        [ReadOnly] public ComponentTypeHandle<EnemyFlockNextPose> NextPoseHandle;

        /// <summary>读取实体对应的群游槽位。</summary>
        [ReadOnly] public ComponentTypeHandle<EnemyFlockBridgeIndex> BridgeIndexHandle;

        /// <summary>按群游槽位写入姿态，实体之间依靠唯一槽位保证不冲突。</summary>
        [NativeDisableParallelForRestriction]
        public NativeArray<EnemyFlockPose> Poses;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var transforms = chunk.GetNativeArray(ref LocalTransformHandle);
            var nextPoses = chunk.GetNativeArray(ref NextPoseHandle);
            var bridgeIndices = chunk.GetNativeArray(ref BridgeIndexHandle);
            var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

            while (enumerator.NextEntityIndex(out int entityIndex))
            {
                int slot = bridgeIndices[entityIndex].FlockSlot;
                if ((uint)slot >= EnemyFlockLimits.MaximumAgents)
                {
                    continue;
                }

                EnemyFlockNextPose pose = nextPoses[entityIndex];
                transforms[entityIndex] = new LocalTransform
                {
                    Position = pose.Position,
                    Rotation = pose.Rotation,
                    Scale = transforms[entityIndex].Scale,
                };
                Poses[slot] = new EnemyFlockPose
                {
                    Position = pose.Position,
                    Rotation = pose.Rotation,
                };
            }
        }
    }

    /// <summary>按 Transform 索引读取槽位姿态并更新托管对象。</summary>
    [BurstCompile]
    internal struct EnemyFlockApplyTransformJob : IJobParallelForTransform
    {
        /// <summary>按群游槽位读取模拟姿态。</summary>
        [ReadOnly] public NativeArray<EnemyFlockPose> Poses;

        /// <summary>TransformAccessArray 索引到群游槽位的映射。</summary>
        [ReadOnly] public NativeArray<int> SlotsByTransformIndex;

        /// <summary>标记对应 Transform 是否仍由 ECS 控制。</summary>
        [ReadOnly] public NativeArray<byte> ActiveByTransformIndex;

        public void Execute(int index, TransformAccess transform)
        {
            if (ActiveByTransformIndex[index] == 0)
            {
                return;
            }

            int slot = SlotsByTransformIndex[index];
            if ((uint)slot >= EnemyFlockLimits.MaximumAgents)
            {
                return;
            }

            EnemyFlockPose pose = Poses[slot];
            transform.position = pose.Position;
            transform.rotation = pose.Rotation;
        }
    }
}
