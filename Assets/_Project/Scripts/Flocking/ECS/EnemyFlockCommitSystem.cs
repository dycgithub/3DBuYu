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
                _transforms.Dispose();
            if (_slotsByTransformIndex.IsCreated)
                _slotsByTransformIndex.Dispose();
            if (_activeByTransformIndex.IsCreated)
                _activeByTransformIndex.Dispose();
            if (_poses.IsCreated)
                _poses.Dispose();
            _slotOrder.Clear();
        }

        public int RegisterTransform(Transform target, int slot)
        {
            Dependency.Complete();
            if (!target || _transforms.length >= _maximumAgents)
                return -1;

            _transforms.Add(target);
            int transformIndex = _transforms.length - 1;
            _slotOrder.Add(slot);
            _slotsByTransformIndex[transformIndex] = slot;
            _activeByTransformIndex[transformIndex] = 0;
            return transformIndex;
        }

        public void ConfigureCapacity(int maximumAgents)
        {
            Dependency.Complete();
            _maximumAgents = Mathf.Clamp(maximumAgents, 1, EnemyFlockLimits.MaximumAgents);
        }

        public void SetTransformActive(int transformIndex, bool active)
        {
            Dependency.Complete();
            if (transformIndex < 0 || transformIndex >= _transforms.length)
                return;

            _activeByTransformIndex[transformIndex] = active ? (byte)1 : (byte)0;
        }

        public int UnregisterTransform(int transformIndex)
        {
            Dependency.Complete();
            if (transformIndex < 0 || transformIndex >= _transforms.length)
                return -1;

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
                return;

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
                    continue;

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
                return;

            int slot = SlotsByTransformIndex[index];
            if ((uint)slot >= EnemyFlockLimits.MaximumAgents)
                return;

            EnemyFlockPose pose = Poses[slot];
            transform.position = pose.Position;
            transform.rotation = pose.Rotation;
        }
    }
}
