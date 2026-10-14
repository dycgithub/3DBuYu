using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using GeometricMotion;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using SpatialIndexing;

namespace FlockingSystem.ECS
{
    /// <summary>
    /// 调度敌人群游的快照、空间网格和移动模式模拟 Job。
    /// 模拟阶段只生成 ECS 下一姿态，由提交系统负责最终 Transform 写入。
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(GeometricMotionSystem))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct EnemyFlockSimulationSystem : ISystem
    {
        /// <summary>启用代理的群游模拟查询。</summary>
        private EntityQuery _agentQuery;

        /// <summary>唯一的世界配置查询。</summary>
        private EntityQuery _configQuery;

        private ComponentTypeHandle<LocalTransform> _localTransformHandle;
        private ComponentTypeHandle<EnemyFlockAgent> _agentReadOnlyHandle;
        private ComponentTypeHandle<EnemyFlockAgent> _agentReadWriteHandle;
        private ComponentTypeHandle<EnemyFlockNextPose> _nextPoseHandle;
        private ComponentTypeHandle<GeometricMotionInput> _motionInputHandle;
        private ComponentTypeHandle<EnemyFlockBridgeIndex> _bridgeIndexHandle;
        private ComponentTypeHandle<EnemyFlockChunkGoal> _chunkGoalHandle;
        private EntityTypeHandle _entityHandle;

        /// <summary>创建代理查询、组件句柄和共享空间索引系统。</summary>
        public void OnCreate(ref SystemState state)
        {
            _agentQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<LocalTransform>(),
                ComponentType.ReadOnly<EnemyFlockAgent>(),
                ComponentType.ReadWrite<EnemyFlockNextPose>(),
                ComponentType.ReadWrite<GeometricMotionInput>(),
                ComponentType.ReadOnly<EnemyFlockBridgeIndex>(),
                ComponentType.ReadOnly<EnemyFlockActive>(),
                ComponentType.ChunkComponent<EnemyFlockChunkGoal>());
            _configQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<EnemyFlockWorldConfig>());

            _localTransformHandle = state.GetComponentTypeHandle<LocalTransform>(true);
            _agentReadOnlyHandle = state.GetComponentTypeHandle<EnemyFlockAgent>(true);
            _agentReadWriteHandle = state.GetComponentTypeHandle<EnemyFlockAgent>(false);
            _nextPoseHandle = state.GetComponentTypeHandle<EnemyFlockNextPose>(false);
            _motionInputHandle = state.GetComponentTypeHandle<GeometricMotionInput>(false);
            _bridgeIndexHandle = state.GetComponentTypeHandle<EnemyFlockBridgeIndex>(true);
            _chunkGoalHandle = state.GetComponentTypeHandle<EnemyFlockChunkGoal>(false);
            _entityHandle = state.GetEntityTypeHandle();

            state.World.GetOrCreateSystemManaged<SpatialIndexSystem>();
        }

        /// <summary>等待本系统提交的 Job，交由共享空间网格负责数组释放。</summary>
        public void OnDestroy(ref SystemState state)
        {
            state.Dependency.Complete();
        }

        /// <summary>
        /// 采集位置和速度快照，构建空间网格，再并行计算下一姿态。
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
            if (_configQuery.IsEmptyIgnoreFilter || _agentQuery.CalculateEntityCount() == 0)
            {
                return;
            }

            EnemyFlockWorldConfig config = _configQuery.GetSingleton<EnemyFlockWorldConfig>();
            SpatialIndexSystem spatial = state.World.GetExistingSystemManaged<SpatialIndexSystem>();
            ContinuousSpatialGrid grid = spatial.Enemies;
            // Configure 等待上一帧网格读者；其余 ECS 依赖保留到后续 Job 调度，避免主线程提前阻塞。
            grid.Configure(config.GridOrigin, config.GridCellSize, config.GridDimensions, config.AgentCapacity);
            spatial.Service?.CaptureEnemies();
            _localTransformHandle.Update(ref state);
            _agentReadOnlyHandle.Update(ref state);
            _agentReadWriteHandle.Update(ref state);
            _nextPoseHandle.Update(ref state);
            _motionInputHandle.Update(ref state);
            _bridgeIndexHandle.Update(ref state);
            _chunkGoalHandle.Update(ref state);
            _entityHandle.Update(ref state);

            JobHandle dependency = JobHandle.CombineDependencies(state.Dependency, grid.Fence);
            if (spatial.Service == null)
            {
                dependency = new EnemyFlockClearSnapshotJob
                {
                    Active = grid.Active,
                }.Schedule(grid.Active.Length, 64, dependency);
            }

            dependency = new EnemyFlockGatherJob
            {
                LocalTransformHandle = _localTransformHandle,
                AgentHandle = _agentReadOnlyHandle,
                BridgeIndexHandle = _bridgeIndexHandle,
                Active = grid.Active,
                Positions = grid.Positions,
                Velocities = grid.Velocities,
                UseSpatialSlots = spatial.Service != null,
            }.ScheduleParallel(_agentQuery, dependency);

            dependency = grid.Build(dependency);
            spatial.Service?.MarkEnemyGridBuilt();

            state.Dependency = new EnemyFlockStepJob
            {
                EntityHandle = _entityHandle,
                LocalTransformHandle = _localTransformHandle,
                AgentHandle = _agentReadWriteHandle,
                NextPoseHandle = _nextPoseHandle,
                MotionInputHandle = _motionInputHandle,
                BridgeIndexHandle = _bridgeIndexHandle,
                ChunkGoalHandle = _chunkGoalHandle,
                Active = grid.Active,
                Positions = grid.Positions,
                Velocities = grid.Velocities,
                CellStarts = grid.Starts,
                SortedSlots = grid.Slots,
                UseSpatialSlots = spatial.Service != null,
                Config = config,
                Grid = new GridLayout(
                    config.GridOrigin,
                    config.GridCellSize,
                    config.GridDimensions),
                DeltaTime = math.min(SystemAPI.Time.DeltaTime, config.MaxDeltaTime),
            }.ScheduleParallel(_agentQuery, dependency);
            grid.Fence = state.Dependency;
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
        public ComponentTypeHandle<GeometricMotionInput> MotionInputHandle;
        [ReadOnly] public ComponentTypeHandle<EnemyFlockBridgeIndex> BridgeIndexHandle;
        public ComponentTypeHandle<EnemyFlockChunkGoal> ChunkGoalHandle;

        /// <summary>快照槽位的有效标记。</summary>
        [ReadOnly] public NativeArray<byte> Active;

        /// <summary>按空间槽位保存的代理位置。</summary>
        [ReadOnly] public NativeArray<float3> Positions;

        /// <summary>按空间槽位保存的代理速度。</summary>
        [ReadOnly] public NativeArray<float3> Velocities;

        /// <summary>每个网格单元在 SortedSlots 中的起止区间。</summary>
        [ReadOnly] public NativeArray<int> CellStarts;

        /// <summary>按网格单元连续排列的代理槽位。</summary>
        [ReadOnly] public NativeArray<int> SortedSlots;

        private const float MinimumVelocitySquared = 0.0001f;

        /// <summary>本次模拟使用的世界配置快照。</summary>
        public EnemyFlockWorldConfig Config;

        /// <summary>本次模拟使用的只读网格布局，避免每个代理重复构造。</summary>
        public GridLayout Grid;

        /// <summary>是否使用托管空间服务提供的位置快照。</summary>
        public bool UseSpatialSlots;

        /// <summary>经过世界配置限制后的模拟步长，单位为秒。</summary>
        public float DeltaTime;

        /// <summary>处理一个 ECS Chunk；Chunk 目标只在该 Chunk 内共享。</summary>
        public void Execute(
            in ArchetypeChunk chunk,
            int unfilteredChunkIndex,
            bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            EnemyFlockChunkGoal goal = UpdateChunkGoal(chunk, unfilteredChunkIndex);

            var transforms = chunk.GetNativeArray(ref LocalTransformHandle);
            var agents = chunk.GetNativeArray(ref AgentHandle);
            var nextPoses = chunk.GetNativeArray(ref NextPoseHandle);
            var motionInputs = chunk.GetNativeArray(ref MotionInputHandle);
            var bridgeIndices = chunk.GetNativeArray(ref BridgeIndexHandle);
            var entityEnumerator = new ChunkEntityEnumerator(
                useEnabledMask,
                chunkEnabledMask,
                chunk.Count);

            while (entityEnumerator.NextEntityIndex(out int entityIndex))
            {
                if (!TryGetAgentSnapshot(
                        bridgeIndices[entityIndex],
                        transforms[entityIndex].Position,
                        out float3 position,
                        out int steeringSlot))
                {
                    continue;
                }

                EnemyFlockAgent agent = agents[entityIndex];
                GeometricMotionInput motionInput = default;
                EnemyFlockNextPose nextPose = SimulateAgent(
                    ref agent,
                    ref motionInput,
                    position,
                    transforms[entityIndex].Rotation,
                    steeringSlot,
                    goal.GoalPosition);
                agents[entityIndex] = agent;
                motionInputs[entityIndex] = motionInput;
                nextPoses[entityIndex] = nextPose;
            }
        }

        /// <summary>初始化或推进 Chunk 目标点及其随机状态。</summary>
        private EnemyFlockChunkGoal UpdateChunkGoal(
            in ArchetypeChunk chunk,
            int unfilteredChunkIndex)
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
            {
                goal.GoalPosition = GetRandomGoal(ref random);
            }

            goal.RandomState = random.state;
            chunk.SetChunkComponentData(ref ChunkGoalHandle, goal);
            return goal;
        }

        /// <summary>选择托管空间快照或 ECS 当前姿态，并返回邻居查询槽位。</summary>
        private bool TryGetAgentSnapshot(
            EnemyFlockBridgeIndex bridgeIndex,
            float3 fallbackPosition,
            out float3 position,
            out int steeringSlot)
        {
            int flockSlot = bridgeIndex.FlockSlot;
            int spatialSlot = bridgeIndex.SpatialSlot;
            bool hasSpatialSnapshot = UseSpatialSlots &&
                (uint)spatialSlot < (uint)Active.Length &&
                Active[spatialSlot] != 0;

            // 没有托管空间服务时，缺失快照代表代理已被移除；跳过它可避免提交旧姿态。
            if (!UseSpatialSlots &&
                ((uint)flockSlot >= (uint)Active.Length || Active[flockSlot] == 0))
            {
                position = float3.zero;
                steeringSlot = -1;
                return false;
            }

            position = hasSpatialSnapshot
                ? Positions[spatialSlot]
                : fallbackPosition;
            steeringSlot = UseSpatialSlots
                ? (hasSpatialSnapshot ? spatialSlot : -1)
                : flockSlot;
            return true;
        }

        /// <summary>消费代理运动命令，并根据移动模式生成下一姿态。</summary>
        /// <param name="agent">要更新的敌人群游和运动数据。</param>
        /// <param name="position">本步使用的世界位置。</param>
        /// <param name="rotation">本步使用的世界旋转。</param>
        /// <param name="steeringSlot">邻居查询使用的槽位。</param>
        /// <param name="goalPosition">当前群游目标点。</param>
        /// <returns>由提交系统应用的下一姿态。</returns>
        private EnemyFlockNextPose SimulateAgent(
            ref EnemyFlockAgent agent,
            ref GeometricMotionInput motionInput,
            float3 position,
            quaternion rotation,
            int steeringSlot,
            float3 goalPosition)
        {
            float3 externalDisplacement = agent.ExternalImpulse * math.max(0f, DeltaTime) + agent.ExternalDisplacement;
            agent.ExternalImpulse = float3.zero;
            agent.ExternalDisplacement = float3.zero;
            motionInput.DeltaTime = DeltaTime;

            if (agent.MotionMode == EnemyFlockMotionMode.Frozen)
            {
                return CreatePose(position, rotation);
            }

            if (agent.MotionMode == EnemyFlockMotionMode.Directed)
            {
                float3 directedDisplacement = agent.DirectedDisplacement;
                agent.DirectedDisplacement = float3.zero;
                motionInput.Velocity = DeltaTime > 0f
                    ? directedDisplacement / DeltaTime
                    : float3.zero;
                return new EnemyFlockNextPose
                {
                    Position = position + directedDisplacement + externalDisplacement,
                    Rotation = agent.DirectedRotation,
                };
            }

            if (agent.MotionMode != EnemyFlockMotionMode.Flocking)
            {
                return CreatePose(position, rotation);
            }

            agent.DirectedDisplacement = float3.zero;
            float3 previousVelocity = agent.Velocity;
            float3 velocity = agent.Velocity;
            if (math.lengthsq(velocity) <= MinimumVelocitySquared)
            {
                float speed = math.clamp(agent.Speed, agent.MinSpeed, agent.MaxSpeed);
                velocity = math.mul(rotation, new float3(0f, 0f, speed));
            }

            float3 acceleration = CalculateSteering(
                steeringSlot,
                position,
                velocity,
                agent,
                goalPosition);
            velocity = EnemyFlockBoidsMath.IntegrateVelocity(
                velocity,
                acceleration,
                DeltaTime,
                agent.MinSpeed,
                agent.MaxSpeed,
                Config.MaxAcceleration);
            agent.Velocity = velocity;
            agent.Speed = math.length(velocity);

            float speedMultiplier = math.max(0f, agent.SpeedMultiplier);
            float3 worldAcceleration = DeltaTime > 0f
                ? (velocity - previousVelocity) * speedMultiplier / DeltaTime
                : float3.zero;
            motionInput.Velocity = velocity * speedMultiplier;
            motionInput.LocalAcceleration = math.mul(
                math.inverse(rotation),
                worldAcceleration);

            bool outsideBounds = math.any(
                position < Config.SwimCenter - Config.SwimLimits)
                || math.any(position > Config.SwimCenter + Config.SwimLimits);
            float rotationSpeed = outsideBounds
                ? agent.RotationSpeed * Config.OutsideBoundsRotationMultiplier
                : agent.RotationSpeed;

            if (math.lengthsq(velocity) > MinimumVelocitySquared)
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

            // 群游位移来自 Boids 速度；外部命令在此步只消费一次。
            return new EnemyFlockNextPose
            {
                Position = position + EnemyFlockBoidsMath.CalculateMovementDelta(
                    agent,
                    DeltaTime) + externalDisplacement,
                Rotation = rotation,
            };
        }

        /// <summary>在相邻网格单元内收集有限候选，并合成 Boids 转向向量。</summary>
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
            float cellSize = Grid.CellSize;
            int cellRadius = (int)math.ceil(interactionDistance / cellSize);
            int3 dimensions = Grid.Dimensions;
            int maximumCellRadius = math.max(
                dimensions.x,
                math.max(dimensions.y, dimensions.z));
            cellRadius = math.min(cellRadius, maximumCellRadius);
            int candidateLimit = math.max(0, Config.MaximumNeighbourCandidates);
            int3 centerCell = Grid.Cell(position);
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
                        {
                            continue;
                        }

                        int cellIndex = cell.x + dimensions.x
                            * (cell.y + dimensions.y * cell.z);
                        int start = CellStarts[cellIndex];
                        int end = CellStarts[cellIndex + 1];

                        for (int index = start; index < end && examinedCount < candidateLimit; index++)
                        {
                            examinedCount++;
                            int otherSlot = SortedSlots[index];
                            if (otherSlot == slot
                                || (uint)otherSlot >= (uint)Active.Length
                                || Active[otherSlot] == 0)
                            {
                                continue;
                            }

                            float3 offsetToOther = Positions[otherSlot] - position;
                            float distanceSquared = math.lengthsq(offsetToOther);

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
                                    separationDistance,
                                    slot,
                                    otherSlot);
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
                + EnemyFlockBoidsMath.CalculateSeparationForce(
                    separation,
                    Config.SeparationWeight)
                + math.normalizesafe(goal, float3.zero) * Config.GoalWeight
                + math.normalizesafe(boundary, float3.zero) * Config.BoundaryWeight;
        }

        /// <summary>创建只保留当前位置和旋转的下一姿态。</summary>
        /// <param name="position">世界坐标位置。</param>
        /// <param name="rotation">世界坐标旋转。</param>
        /// <returns>位置和旋转均不变的下一姿态。</returns>
        private static EnemyFlockNextPose CreatePose(float3 position, quaternion rotation) =>
            new() { Position = position, Rotation = rotation };

        /// <summary>在游动区域内生成一个随机世界目标点。</summary>
        private float3 GetRandomGoal(ref Random random)
        {
            return Config.SwimCenter + new float3(
                random.NextFloat(-Config.SwimLimits.x, Config.SwimLimits.x),
                random.NextFloat(-Config.SwimLimits.y, Config.SwimLimits.y),
                random.NextFloat(-Config.SwimLimits.z, Config.SwimLimits.z));
        }
    }
}
