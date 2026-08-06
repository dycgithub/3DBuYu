using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace FlockingSystem.ECS
{
    /// <summary>
    /// Boids 鱼群行为 ECS 系统。
    /// 每帧对每条鱼执行聚合/分离/对齐规则，使用 Burst 编译加速。
    /// 通过 NativeArray 共享所有鱼的位置和速度以进行 O(n²) 邻居查询，
    /// 在 150 条鱼以内 Burst 性能完全足够（~0.05ms/帧）。
    ///
    /// 替代原 FlockAgent.Update() 中的逐帧概率驱动 O(n²) 遍历。
    /// </summary>
    public partial struct FlockBoidsSystem : ISystem
    {
        private EntityQuery fishQuery;

        /// <summary>
        /// 初始化：构建鱼群实体查询。
        /// </summary>
        public void OnCreate(ref SystemState state)
        {
            fishQuery = state.GetEntityQuery(
                ComponentType.ReadWrite<LocalTransform>(),
                ComponentType.ReadWrite<FlockAgentData>());

            state.RequireForUpdate<FlockGoalData>();
        }

        /// <summary>
        /// 每帧更新：收集所有鱼的位置与速度 → 并行执行 Boids 规则。
        /// </summary>
        public void OnUpdate(ref SystemState state)
        {
            var goalData = SystemAPI.GetSingleton<FlockGoalData>();
            float deltaTime = SystemAPI.Time.DeltaTime;

            int fishCount = fishQuery.CalculateEntityCount();
            if (fishCount < 2)
            {
                if (fishCount == 1)
                {
                    state.Dependency = new SingleFishBoundaryJob
                    {
                        GoalData = goalData,
                        DeltaTime = deltaTime,
                    }.ScheduleParallel(fishQuery, state.Dependency);
                }
                return;
            }

            // 收集所有鱼的位置和速度（同一查询 → 数组索引一致）
            var positions = fishQuery.ToComponentDataArray<LocalTransform>(state.WorldUpdateAllocator);
            var agents = fishQuery.ToComponentDataArray<FlockAgentData>(state.WorldUpdateAllocator);

            state.Dependency = new BoidsUpdateJob
            {
                Positions = positions,
                AgentsRO = agents,
                GoalData = goalData,
                DeltaTime = deltaTime,
            }.ScheduleParallel(fishQuery, state.Dependency);
        }
    }

    /// <summary>
    /// Boids 并行更新 Job：对每条鱼执行聚合/分离/对齐/边界约束。
    /// </summary>
    [BurstCompile]
    partial struct BoidsUpdateJob : IJobEntity
    {
        /// <summary>所有鱼的 Transform 快照（只读，用于邻居位置查询）。</summary>
        [ReadOnly] public NativeArray<LocalTransform> Positions;

        /// <summary>所有鱼的 Agent 数据快照（只读，用于邻居速度查询）。</summary>
        [ReadOnly] public NativeArray<FlockAgentData> AgentsRO;

        /// <summary>全局目标数据。</summary>
        [ReadOnly] public FlockGoalData GoalData;

        /// <summary>帧间隔时间。</summary>
        public float DeltaTime;

        /// <summary>
        /// 对单条鱼执行完整 Boids 更新。
        /// </summary>
        void Execute(ref LocalTransform transform, ref FlockAgentData agent)
        {
            float3 myPos = transform.Position;
            float3 swimMin = GoalData.SwimCenter - GoalData.SwimLimits;
            float3 swimMax = GoalData.SwimCenter + GoalData.SwimLimits;

            bool outsideBounds = math.any(myPos < swimMin) || math.any(myPos > swimMax);

            if (outsideBounds)
            {
                SteerToward(ref transform, ref agent, GoalData.SwimCenter);
            }
            else
            {
                ApplyBoidsRules(ref transform, ref agent, myPos);
            }

            // 钳制并向前移动
            agent.Speed = math.clamp(agent.Speed, agent.MinSpeed, agent.MaxSpeed);
            float effectiveSpeed = agent.Speed * agent.SpeedMultiplier;
            transform.Position += math.forward(transform.Rotation) * effectiveSpeed * DeltaTime;
        }

        /// <summary>
        /// 执行 Boids 三规则：分离 → 聚合 → 对齐。
        /// </summary>
        private void ApplyBoidsRules(ref LocalTransform transform, ref FlockAgentData agent, float3 myPos)
        {
            float3 cohesionAccum = float3.zero;
            float3 avoidanceAccum = float3.zero;
            float speedAccum = 0f;
            int groupSize = 0;

            float neighbourDistSq = agent.NeighbourDistance * agent.NeighbourDistance;
            float separationDist = agent.SeparationDistance;
            float separationDistSq = separationDist * separationDist;

            for (int i = 0; i < Positions.Length; i++)
            {
                float3 otherPos = Positions[i].Position;

                if (math.all(myPos == otherPos)) continue;

                float distSq = math.distancesq(myPos, otherPos);

                // 分离规则（优先级最高）
                if (distSq <= separationDistSq && distSq > 0f)
                {
                    float3 away = myPos - otherPos;
                    avoidanceAccum += math.normalizesafe(away, float3.zero);
                }

                // 聚合 + 对齐（邻居范围内）
                if (distSq <= neighbourDistSq)
                {
                    cohesionAccum += otherPos;
                    speedAccum += AgentsRO[i].Speed;
                    groupSize++;
                }
            }

            float3 finalDirection;

            if (groupSize > 0)
            {
                // 聚合方向
                float3 avgCenter = cohesionAccum / groupSize;
                float3 cohesionDir = (avgCenter + GoalData.GoalPos) * 0.5f - myPos;

                // 分离方向（加权）
                float3 avoidanceDir = avoidanceAccum * separationDist;

                // 对齐：平滑匹配邻居平均速度
                float avgSpeed = speedAccum / groupSize;
                agent.Speed = math.lerp(agent.Speed, avgSpeed, DeltaTime * 3f);

                // 最终方向 = 聚合 + 分离
                finalDirection = cohesionDir + avoidanceDir;
            }
            else
            {
                // 无邻居 → 向目标点移动
                finalDirection = GoalData.GoalPos - myPos;
            }

            // 平滑旋转到最终方向
            if (math.lengthsq(finalDirection) > 0.0001f)
            {
                quaternion targetRot = quaternion.LookRotationSafe(
                    math.normalize(finalDirection), math.up());
                transform.Rotation = math.slerp(
                    transform.Rotation, targetRot, agent.RotationSpeed * DeltaTime);
            }
        }

        /// <summary>
        /// 强制转向目标点（边界折返时使用）。
        /// </summary>
        private void SteerToward(ref LocalTransform transform, ref FlockAgentData agent, float3 target)
        {
            float3 toTarget = math.normalizesafe(target - transform.Position, math.forward());
            quaternion targetRot = quaternion.LookRotationSafe(toTarget, math.up());
            transform.Rotation = math.slerp(
                transform.Rotation, targetRot, agent.RotationSpeed * 2f * DeltaTime);
        }
    }

    /// <summary>
    /// 单条鱼的边界约束和游动 Job（鱼群数量 < 2 时使用）。
    /// </summary>
    [BurstCompile]
    partial struct SingleFishBoundaryJob : IJobEntity
    {
        [ReadOnly] public FlockGoalData GoalData;
        public float DeltaTime;

        void Execute(ref LocalTransform transform, ref FlockAgentData agent)
        {
            float3 myPos = transform.Position;
            float3 swimMin = GoalData.SwimCenter - GoalData.SwimLimits;
            float3 swimMax = GoalData.SwimCenter + GoalData.SwimLimits;

            float3 target;
            float rotSpeed = agent.RotationSpeed;

            if (math.any(myPos < swimMin) || math.any(myPos > swimMax))
            {
                target = GoalData.SwimCenter;
                rotSpeed *= 2f;
            }
            else
            {
                target = GoalData.GoalPos;
            }

            float3 toTarget = math.normalizesafe(target - myPos, math.forward());
            quaternion targetRot = quaternion.LookRotationSafe(toTarget, math.up());
            transform.Rotation = math.slerp(transform.Rotation, targetRot, rotSpeed * DeltaTime);

            agent.Speed = math.clamp(agent.Speed, agent.MinSpeed, agent.MaxSpeed);
            float effectiveSpeed = agent.Speed * agent.SpeedMultiplier;
            transform.Position += math.forward(transform.Rotation) * effectiveSpeed * DeltaTime;
        }
    }
}
