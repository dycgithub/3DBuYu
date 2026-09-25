using Unity.Mathematics;

namespace FlockingSystem.ECS
{
    /// <summary>
    /// 提供 Burst Job 与 EditMode 测试共用的无状态 Boids 数学运算。
    /// 不保存实体、Chunk 或 NativeArray，邻居收集由模拟 Job 负责。
    /// </summary>
    public static class EnemyFlockBoidsMath
    {
        private const float Epsilon = 0.0001f;

        /// <summary>计算一个邻居对当前实体产生的分离贡献。</summary>
        /// <param name="position">当前实体的世界坐标。</param>
        /// <param name="otherPosition">邻居实体的世界坐标。</param>
        /// <param name="separationDistance">分离作用半径，单位为世界单位。</param>
        /// <returns>指向远离邻居方向的未加权分离向量。</returns>
        public static float3 CalculateSeparationContribution(
            float3 position,
            float3 otherPosition,
            float separationDistance)
        {
            return CalculateSeparationContribution(
                position,
                otherPosition,
                separationDistance,
                -1,
                -1);
        }

        /// <summary>计算分离贡献；重合时使用 slot 生成彼此相反的稳定方向。</summary>
        /// <param name="position">当前实体的世界坐标。</param>
        /// <param name="otherPosition">邻居实体的世界坐标。</param>
        /// <param name="separationDistance">分离作用半径，单位为世界单位。</param>
        /// <param name="slot">当前实体槽位；重合时用于生成稳定方向。</param>
        /// <param name="otherSlot">邻居实体槽位；重合时用于生成稳定方向。</param>
        /// <returns>距离越近贡献越强的分离向量。</returns>
        public static float3 CalculateSeparationContribution(
            float3 position,
            float3 otherPosition,
            float separationDistance,
            int slot,
            int otherSlot)
        {
            float radius = math.max(0f, separationDistance);
            float3 offset = position - otherPosition;
            float distanceSquared = math.lengthsq(offset);
            float radiusSquared = radius * radius;

            if (radius <= Epsilon || distanceSquared > radiusSquared)
            {
                return float3.zero;
            }

            if (distanceSquared <= Epsilon * Epsilon)
            {
                if (slot < 0 || otherSlot < 0 || slot == otherSlot)
                {
                    return float3.zero;
                }

                return GetStablePairDirection(slot, otherSlot) / Epsilon;
            }

            float distance = math.sqrt(distanceSquared);
            float falloff = 1f - distance / math.max(radius, Epsilon);
            return math.normalizesafe(offset, float3.zero) * (falloff / distance);
        }

        /// <summary>按分离权重缩放分离贡献，不抹掉近距离贡献的强弱差异。</summary>
        /// <param name="separation">邻居贡献累加得到的分离向量。</param>
        /// <param name="separationWeight">分离行为权重。</param>
        /// <returns>加权后的分离向量。</returns>
        public static float3 CalculateSeparationForce(float3 separation, float separationWeight)
        {
            return separation * math.max(0f, separationWeight);
        }

        /// <summary>计算 ECS 代理在一个模拟步中的速度位移和击退位移。</summary>
        /// <param name="agent">包含速度倍率和外部冲量的 ECS 代理。</param>
        /// <param name="deltaTime">模拟步长。</param>
        /// <returns>本模拟步应提交到下一姿态的位置增量。</returns>
        public static float3 CalculateMovementDelta(
            EnemyFlockAgent agent,
            float deltaTime)
        {
            float step = math.max(0f, deltaTime);
            float multiplier = math.max(0f, agent.SpeedMultiplier);
            return (agent.Velocity * multiplier + agent.ExternalImpulse) * step;
        }

        /// <summary>计算指向邻居平均位置的聚拢向量。</summary>
        /// <param name="position">当前实体的世界坐标。</param>
        /// <param name="averagePosition">邻居位置平均值。</param>
        /// <returns>从当前实体指向邻居平均位置的向量。</returns>
        public static float3 CalculateCohesion(float3 position, float3 averagePosition)
        {
            return averagePosition - position;
        }

        /// <summary>计算当前速度向邻居平均速度靠拢的对齐向量。</summary>
        /// <param name="velocity">当前实体速度。</param>
        /// <param name="averageVelocity">邻居速度平均值。</param>
        /// <returns>从当前速度指向邻居平均速度的向量。</returns>
        public static float3 CalculateAlignment(float3 velocity, float3 averageVelocity)
        {
            return averageVelocity - velocity;
        }

        /// <summary>
        /// 计算把实体推回游泳区域的边界力。
        /// 边界力会在边界预警距离内逐渐生效，越界后继续指向区域内部。
        /// </summary>
        /// <param name="position">实体世界坐标。</param>
        /// <param name="config">游动区域和边界预警距离。</param>
        /// <returns>未加权的边界回推向量。</returns>
        public static float3 CalculateBoundaryForce(
            float3 position,
            EnemyFlockWorldConfig config)
        {
            float3 limits = math.max(config.SwimLimits, new float3(Epsilon));
            float3 minimum = config.SwimCenter - limits;
            float3 maximum = config.SwimCenter + limits;
            float margin = math.max(config.BoundaryMargin, Epsilon);

            return new float3(
                CalculateAxisBoundaryForce(position.x, minimum.x, maximum.x, margin),
                CalculateAxisBoundaryForce(position.y, minimum.y, maximum.y, margin),
                CalculateAxisBoundaryForce(position.z, minimum.z, maximum.z, margin));
        }

        /// <summary>以最大加速度积分速度，并将最终速度限制在配置范围内。</summary>
        /// <param name="velocity">当前速度。</param>
        /// <param name="acceleration">当前步的合成加速度。</param>
        /// <param name="deltaTime">模拟步长，单位为秒。</param>
        /// <param name="minSpeed">最小速度，单位为世界单位/秒。</param>
        /// <param name="maxSpeed">最大速度，单位为世界单位/秒。</param>
        /// <param name="maxAcceleration">最大加速度，单位为世界单位/秒²。</param>
        /// <returns>限制在速度和加速度范围内的下一速度。</returns>
        public static float3 IntegrateVelocity(
            float3 velocity,
            float3 acceleration,
            float deltaTime,
            float minSpeed,
            float maxSpeed,
            float maxAcceleration)
        {
            float3 limitedAcceleration = ClampMagnitude(acceleration, maxAcceleration);
            float3 nextVelocity = velocity + limitedAcceleration * math.max(0f, deltaTime);
            float minimumSpeed = math.max(0f, minSpeed);
            float maximumSpeed = math.max(minimumSpeed, maxSpeed);
            float speed = math.length(nextVelocity);

            if (speed <= Epsilon)
            {
                if (minimumSpeed <= Epsilon)
                {
                    return float3.zero;
                }

                return new float3(0f, 0f, minimumSpeed);
            }

            return nextVelocity / speed * math.clamp(speed, minimumSpeed, maximumSpeed);
        }

        /// <summary>将向量长度限制在指定最大值内。</summary>
        /// <param name="value">待限制的向量。</param>
        /// <param name="maximumLength">允许的最大长度。</param>
        /// <returns>长度不超过最大值的向量。</returns>
        public static float3 ClampMagnitude(float3 value, float maximumLength)
        {
            float maxLength = math.max(0f, maximumLength);
            float lengthSquared = math.lengthsq(value);
            float maxLengthSquared = maxLength * maxLength;

            if (lengthSquared <= maxLengthSquared || lengthSquared <= Epsilon * Epsilon)
            {
                return value;
            }

            return value * (maxLength / math.sqrt(lengthSquared));
        }

        /// <summary>根据世界种子和 Chunk 首实体索引生成稳定且非零的 Chunk 随机种子。</summary>
        /// <param name="randomSeed">世界级随机种子。</param>
        /// <param name="firstEntityIndex">Chunk 首实体索引。</param>
        /// <returns>保证非零且对输入稳定的随机种子。</returns>
        public static uint GetChunkSeed(uint randomSeed, int firstEntityIndex)
        {
            uint value = randomSeed ^ ((uint)math.max(0, firstEntityIndex) + 1u) * 747796405u;
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            value *= 3266489917u;
            value ^= value >> 16;
            return value == 0 ? 1u : value;
        }

        /// <summary>把单轴位置转换为指向边界内部的分段力。</summary>
        private static float CalculateAxisBoundaryForce(
            float coordinate,
            float minimum,
            float maximum,
            float margin)
        {
            float halfExtent = math.max((maximum - minimum) * 0.5f, Epsilon);
            float safeMargin = math.min(math.max(margin, Epsilon), halfExtent);

            if (coordinate < minimum)
            {
                return 1f + (minimum - coordinate) / safeMargin;
            }

            if (coordinate > maximum)
            {
                return -1f - (coordinate - maximum) / safeMargin;
            }

            if (coordinate < minimum + safeMargin)
            {
                return (minimum + safeMargin - coordinate) / safeMargin;
            }

            if (coordinate > maximum - safeMargin)
            {
                return -(coordinate - (maximum - safeMargin)) / safeMargin;
            }

            return 0f;
        }

        /// <summary>为重合代理生成可复现且彼此相反的分离方向。</summary>
        private static float3 GetStablePairDirection(int slot, int otherSlot)
        {
            int lowerSlot = math.min(slot, otherSlot);
            int higherSlot = math.max(slot, otherSlot);
            uint hash = (uint)(lowerSlot + 1) * 747796405u
                ^ (uint)(higherSlot + 1) * 2891336453u;
            hash ^= hash >> 16;
            hash *= 2246822519u;
            hash ^= hash >> 13;

            float3 direction = (hash % 6u) switch
            {
                0u => new float3(1f, 0f, 0f),
                1u => new float3(0f, 1f, 0f),
                2u => new float3(0f, 0f, 1f),
                3u => new float3(-1f, 0f, 0f),
                4u => new float3(0f, -1f, 0f),
                _ => new float3(0f, 0f, -1f),
            };

            return slot < otherSlot ? direction : -direction;
        }
    }
}
