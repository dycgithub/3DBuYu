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
    /// 调度敌人群游的快照、空间网格和 Boids 模拟 Job。
    /// 模拟阶段只写入 ECS 的下一帧姿态，由提交系统负责同步表现对象。
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct EnemyFlockSimulationSystem : ISystem
    {
        private EntityQuery _agentQuery;
        private EntityQuery _configQuery;
        private NativeArray<byte> _active;
        private NativeArray<float3> _positions;
        private NativeArray<float3> _velocities;
        private NativeArray<int> _cellCounts;
        private NativeArray<int> _cellStarts;
        private NativeArray<int> _cellWriteHeads;
        private NativeArray<int> _sortedSlots;
        private ComponentTypeHandle<LocalTransform> _localTransformHandle;
        private ComponentTypeHandle<EnemyFlockAgent> _agentReadOnlyHandle;
        private ComponentTypeHandle<EnemyFlockAgent> _agentReadWriteHandle;
        private ComponentTypeHandle<EnemyFlockNextPose> _nextPoseHandle;
        private ComponentTypeHandle<EnemyFlockBridgeIndex> _bridgeIndexHandle;
        private ComponentTypeHandle<EnemyFlockChunkGoal> _chunkGoalHandle;
        private EntityTypeHandle _entityHandle;

        public void OnCreate(ref SystemState state)
        {
            _agentQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<EnemyFlockAgent>(),
                ComponentType.ReadWrite<EnemyFlockNextPose>(),
                ComponentType.ReadOnly<EnemyFlockBridgeIndex>(),
                ComponentType.ReadOnly<EnemyFlockActive>(),
                ComponentType.ChunkComponent<EnemyFlockChunkGoal>());
            _configQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<EnemyFlockWorldConfig>());

            _localTransformHandle = state.GetComponentTypeHandle<LocalTransform>(true);
            _agentReadOnlyHandle = state.GetComponentTypeHandle<EnemyFlockAgent>(true);
            _agentReadWriteHandle = state.GetComponentTypeHandle<EnemyFlockAgent>(false);
            _nextPoseHandle = state.GetComponentTypeHandle<EnemyFlockNextPose>(false);
            _bridgeIndexHandle = state.GetComponentTypeHandle<EnemyFlockBridgeIndex>(true);
            _chunkGoalHandle = state.GetComponentTypeHandle<EnemyFlockChunkGoal>(false);
            _entityHandle = state.GetEntityTypeHandle();

            _active = new NativeArray<byte>(
                EnemyFlockLimits.MaximumAgents,
                Allocator.Persistent);
            _positions = new NativeArray<float3>(
                EnemyFlockLimits.MaximumAgents,
                Allocator.Persistent);
            _velocities = new NativeArray<float3>(
                EnemyFlockLimits.MaximumAgents,
                Allocator.Persistent);
            _cellCounts = new NativeArray<int>(
                EnemyFlockLimits.MaximumGridCells,
                Allocator.Persistent);
            _cellStarts = new NativeArray<int>(
                EnemyFlockLimits.MaximumGridCells + 1,
                Allocator.Persistent);
            _cellWriteHeads = new NativeArray<int>(
                EnemyFlockLimits.MaximumGridCells,
                Allocator.Persistent);
            _sortedSlots = new NativeArray<int>(
                EnemyFlockLimits.MaximumAgents,
                Allocator.Persistent);
        }

        public void OnDestroy(ref SystemState state)
        {
            state.Dependency.Complete();
            Dispose(ref _active);
            Dispose(ref _positions);
            Dispose(ref _velocities);
            Dispose(ref _cellCounts);
            Dispose(ref _cellStarts);
            Dispose(ref _cellWriteHeads);
            Dispose(ref _sortedSlots);
        }

        public void OnUpdate(ref SystemState state)
        {
            if (_configQuery.IsEmptyIgnoreFilter || _agentQuery.CalculateEntityCount() == 0)
                return;

            EnemyFlockWorldConfig config = _configQuery.GetSingleton<EnemyFlockWorldConfig>();
            _localTransformHandle.Update(ref state);
            _agentReadOnlyHandle.Update(ref state);
            _agentReadWriteHandle.Update(ref state);
            _nextPoseHandle.Update(ref state);
            _bridgeIndexHandle.Update(ref state);
            _chunkGoalHandle.Update(ref state);
            _entityHandle.Update(ref state);

            JobHandle dependency = state.Dependency;
            dependency = new EnemyFlockClearSnapshotJob
            {
                Active = _active,
            }.Schedule(EnemyFlockLimits.MaximumAgents, 64, dependency);

            dependency = new EnemyFlockGatherJob
            {
                LocalTransformHandle = _localTransformHandle,
                AgentHandle = _agentReadOnlyHandle,
                BridgeIndexHandle = _bridgeIndexHandle,
                Active = _active,
                Positions = _positions,
                Velocities = _velocities,
                AgentCapacity = config.AgentCapacity,
            }.ScheduleParallel(_agentQuery, dependency);

            dependency = new EnemyFlockBuildGridJob
            {
                Active = _active,
                Positions = _positions,
                CellCounts = _cellCounts,
                CellStarts = _cellStarts,
                CellWriteHeads = _cellWriteHeads,
                SortedSlots = _sortedSlots,
                Config = config,
                AgentCapacity = config.AgentCapacity,
            }.Schedule(dependency);

            state.Dependency = new EnemyFlockStepJob
            {
                EntityHandle = _entityHandle,
                LocalTransformHandle = _localTransformHandle,
                AgentHandle = _agentReadWriteHandle,
                NextPoseHandle = _nextPoseHandle,
                BridgeIndexHandle = _bridgeIndexHandle,
                ChunkGoalHandle = _chunkGoalHandle,
                Active = _active,
                Positions = _positions,
                Velocities = _velocities,
                CellStarts = _cellStarts,
                SortedSlots = _sortedSlots,
                Config = config,
                DeltaTime = math.min(SystemAPI.Time.DeltaTime, config.MaxDeltaTime),
            }.ScheduleParallel(_agentQuery, dependency);
        }

        private static void Dispose<T>(ref NativeArray<T> array) where T : struct
        {
            if (array.IsCreated)
                array.Dispose();
        }
    }

    /// <summary>
    /// 对每个 ECS Chunk 执行一次 Boids 模拟，并为实体写入下一帧姿态。
    /// 每个 Chunk 的目标点只影响该 Chunk 内的实体；实体邻居则来自空间网格。
    /// </summary>
    [BurstCompile]
    internal struct EnemyFlockStepJob : IJobChunk
    {
        [ReadOnly] public EntityTypeHandle EntityHandle;
        [ReadOnly] public ComponentTypeHandle<LocalTransform> LocalTransformHandle;
        public ComponentTypeHandle<EnemyFlockAgent> AgentHandle;
        public ComponentTypeHandle<EnemyFlockNextPose> NextPoseHandle;
        [ReadOnly] public ComponentTypeHandle<EnemyFlockBridgeIndex> BridgeIndexHandle;
        public ComponentTypeHandle<EnemyFlockChunkGoal> ChunkGoalHandle;

        [ReadOnly] public NativeArray<byte> Active;
        [ReadOnly] public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<float3> Velocities;
        [ReadOnly] public NativeArray<int> CellStarts;
        [ReadOnly] public NativeArray<int> SortedSlots;

        public EnemyFlockWorldConfig Config;
        public float DeltaTime;

        public void Execute(
            in ArchetypeChunk chunk,
            int unfilteredChunkIndex,
            bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            var entities = chunk.GetNativeArray(EntityHandle);
            EnemyFlockChunkGoal goal = chunk.GetChunkComponentData(ref ChunkGoalHandle);
            int seedEntityIndex = entities.Length > 0
                ? entities[0].Index
                : unfilteredChunkIndex;
            uint expectedSeed = EnemyFlockBoidsMath.GetChunkSeed(
                Config.RandomSeed,
                seedEntityIndex);

            if (goal.RandomState == 0 || goal.SeedEntityIndex != seedEntityIndex)
            {
                goal.SeedEntityIndex = seedEntityIndex;
                goal.RandomState = expectedSeed;
                var initialRandom = new Random(goal.RandomState);
                goal.GoalPosition = GetRandomGoal(ref initialRandom);
                goal.RandomState = initialRandom.state;
            }

            var random = new Random(goal.RandomState);
            if (random.NextFloat() < math.saturate(Config.GoalChangeChance))
                goal.GoalPosition = GetRandomGoal(ref random);

            goal.RandomState = random.state;
            chunk.SetChunkComponentData(ref ChunkGoalHandle, goal);

            var transforms = chunk.GetNativeArray(ref LocalTransformHandle);
            var agents = chunk.GetNativeArray(ref AgentHandle);
            var nextPoses = chunk.GetNativeArray(ref NextPoseHandle);
            var bridgeIndices = chunk.GetNativeArray(ref BridgeIndexHandle);
            var entityEnumerator = new ChunkEntityEnumerator(
                useEnabledMask,
                chunkEnabledMask,
                chunk.Count);

            while (entityEnumerator.NextEntityIndex(out int entityIndex))
            {
                int slot = bridgeIndices[entityIndex].Value;
                if ((uint)slot >= (uint)Config.AgentCapacity || Active[slot] == 0)
                    continue;

                EnemyFlockAgent agent = agents[entityIndex];
                float3 position = transforms[entityIndex].Position;
                quaternion rotation = transforms[entityIndex].Rotation;
                float3 velocity = agent.Velocity;
                if (math.lengthsq(velocity) <= 0.0001f)
                {
                    float speed = math.clamp(agent.Speed, agent.MinSpeed, agent.MaxSpeed);
                    velocity = math.mul(rotation, new float3(0f, 0f, speed));
                }

                float3 acceleration = CalculateSteering(
                    slot,
                    position,
                    velocity,
                    agent,
                    goal.GoalPosition);
                velocity = EnemyFlockBoidsMath.IntegrateVelocity(
                    velocity,
                    acceleration,
                    DeltaTime,
                    agent.MinSpeed,
                    agent.MaxSpeed,
                    Config.MaxAcceleration);
                agent.Velocity = velocity;
                agent.Speed = math.length(velocity);

                bool outsideBounds = math.any(
                    position < Config.SwimCenter - Config.SwimLimits)
                    || math.any(position > Config.SwimCenter + Config.SwimLimits);
                float rotationSpeed = outsideBounds
                    ? agent.RotationSpeed * Config.OutsideBoundsRotationMultiplier
                    : agent.RotationSpeed;

                if (math.lengthsq(velocity) > 0.0001f)
                {
                    float3 fallbackForward = math.mul(
                        rotation,
                        new float3(0f, 0f, 1f));
                    quaternion targetRotation = quaternion.LookRotationSafe(
                        math.normalizesafe(velocity, fallbackForward),
                        math.up());
                    rotation = math.slerp(
                        rotation,
                        targetRotation,
                        math.saturate(rotationSpeed * DeltaTime));
                }

                float speedMultiplier = math.max(0f, agent.SpeedMultiplier);
                nextPoses[entityIndex] = new EnemyFlockNextPose
                {
                    Position = position + velocity * speedMultiplier * DeltaTime,
                    Rotation = rotation,
                };
                agents[entityIndex] = agent;
            }
        }

        private float3 CalculateSteering(
            int slot,
            float3 position,
            float3 velocity,
            EnemyFlockAgent agent,
            float3 goalPosition)
        {
            float3 positionSum = float3.zero;
            float3 velocitySum = float3.zero;
            float3 separation = float3.zero;
            int neighbourCount = 0;
            int examinedCount = 0;

            float neighbourDistance = math.max(0f, agent.NeighbourDistance);
            float separationDistance = math.max(0f, agent.SeparationDistance);
            float interactionDistance = math.max(neighbourDistance, separationDistance);
            float cellSize = math.max(Config.GridCellSize, 0.0001f);
            int cellRadius = (int)math.ceil(interactionDistance / cellSize);
            int3 dimensions = math.max(Config.GridDimensions, new int3(1));
            int maximumCellRadius = math.max(
                dimensions.x,
                math.max(dimensions.y, dimensions.z));
            cellRadius = math.min(cellRadius, maximumCellRadius);
            int candidateLimit = math.max(0, Config.MaximumNeighbourCandidates);
            int3 centerCell = EnemyFlockGridMath.GetCell(position, Config);
            float neighbourDistanceSquared = neighbourDistance * neighbourDistance;
            float separationDistanceSquared = separationDistance * separationDistance;

            for (int z = -cellRadius; z <= cellRadius && examinedCount < candidateLimit; z++)
            {
                for (int y = -cellRadius; y <= cellRadius && examinedCount < candidateLimit; y++)
                {
                    for (int x = -cellRadius; x <= cellRadius && examinedCount < candidateLimit; x++)
                    {
                        int3 cell = centerCell + new int3(x, y, z);
                        if (math.any(cell < int3.zero) || math.any(cell >= dimensions))
                            continue;

                        int cellIndex = cell.x + dimensions.x
                            * (cell.y + dimensions.y * cell.z);
                        int start = CellStarts[cellIndex];
                        int end = CellStarts[cellIndex + 1];

                        for (int index = start; index < end && examinedCount < candidateLimit; index++)
                        {
                            examinedCount++;
                            int otherSlot = SortedSlots[index];
                            if (otherSlot == slot
                                || (uint)otherSlot >= (uint)Config.AgentCapacity
                                || Active[otherSlot] == 0)
                                continue;

                            float3 offsetToOther = Positions[otherSlot] - position;
                            float distanceSquared = math.lengthsq(offsetToOther);
                            if (distanceSquared <= 0f)
                                continue;

                            if (distanceSquared <= neighbourDistanceSquared)
                            {
                                positionSum += Positions[otherSlot];
                                velocitySum += Velocities[otherSlot];
                                neighbourCount++;
                            }

                            if (distanceSquared <= separationDistanceSquared)
                            {
                                separation += EnemyFlockBoidsMath.CalculateSeparationContribution(
                                    position,
                                    Positions[otherSlot],
                                    separationDistance);
                            }
                        }
                    }
                }
            }

            float3 cohesion = float3.zero;
            float3 alignment = float3.zero;
            if (neighbourCount > 0)
            {
                float inverseCount = 1f / neighbourCount;
                cohesion = EnemyFlockBoidsMath.CalculateCohesion(
                    position,
                    positionSum * inverseCount);
                alignment = EnemyFlockBoidsMath.CalculateAlignment(
                    velocity,
                    velocitySum * inverseCount);
            }

            float3 goal = goalPosition - position;
            float3 boundary = EnemyFlockBoidsMath.CalculateBoundaryForce(position, Config);
            return math.normalizesafe(cohesion, float3.zero) * Config.CohesionWeight
                + math.normalizesafe(alignment, float3.zero) * Config.AlignmentWeight
                + math.normalizesafe(separation, float3.zero) * Config.SeparationWeight
                + math.normalizesafe(goal, float3.zero) * Config.GoalWeight
                + math.normalizesafe(boundary, float3.zero) * Config.BoundaryWeight;
        }

        private float3 GetRandomGoal(ref Random random)
        {
            return Config.SwimCenter + new float3(
                random.NextFloat(-Config.SwimLimits.x, Config.SwimLimits.x),
                random.NextFloat(-Config.SwimLimits.y, Config.SwimLimits.y),
                random.NextFloat(-Config.SwimLimits.z, Config.SwimLimits.z));
        }
    }
}
