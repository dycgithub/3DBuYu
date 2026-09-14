using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Jobs;

namespace FlockingSystem.ECS
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(EnemyFlockSimulationSystem))]
    public partial class EnemyFlockCommitSystem : SystemBase
    {
        private EntityQuery _query;
        private TransformAccessArray _transforms;
        private NativeArray<int> _slotsByTransformIndex;
        private NativeArray<byte> _activeByTransformIndex;
        private NativeArray<EnemyFlockPose> _poses;
        private readonly List<int> _slotOrder = new(EnemyFlockLimits.MaximumAgents);
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
            _slotOrder.Clear();
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
                return -1;
            }

            _transforms.Add(target);
            int transformIndex = _transforms.length - 1;
            _slotOrder.Add(slot);
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
                return -1;
            }

            int lastIndex = _transforms.length - 1;
            int movedSlot = -1;
            if (transformIndex != lastIndex)
            {
                movedSlot = _slotOrder[lastIndex];
                _slotsByTransformIndex[transformIndex] = movedSlot;
                _activeByTransformIndex[transformIndex] = _activeByTransformIndex[lastIndex];
                _slotOrder[transformIndex] = movedSlot;
            }

            _activeByTransformIndex[lastIndex] = 0;
            _transforms.RemoveAtSwapBack(transformIndex);
            _slotOrder.RemoveAt(lastIndex);
            return movedSlot;
        }

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
        }
    }

    [BurstCompile]
    internal struct EnemyFlockCommitEntitiesJob : IJobChunk
    {
        public ComponentTypeHandle<LocalTransform> LocalTransformHandle;
        [ReadOnly] public ComponentTypeHandle<EnemyFlockNextPose> NextPoseHandle;
        [ReadOnly] public ComponentTypeHandle<EnemyFlockBridgeIndex> BridgeIndexHandle;

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
                int slot = bridgeIndices[entityIndex].Value;
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

    [BurstCompile]
    internal struct EnemyFlockApplyTransformJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<EnemyFlockPose> Poses;
        [ReadOnly] public NativeArray<int> SlotsByTransformIndex;
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
