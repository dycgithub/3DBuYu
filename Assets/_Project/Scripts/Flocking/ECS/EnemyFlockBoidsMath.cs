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

        /// <summary>
        /// 计算一个邻居对当前实体产生的分离贡献。
        /// </summary>
        public static float3 CalculateSeparationContribution(
            float3 position,
            float3 otherPosition,
            float separationDistance)
        {
            float radius = math.max(0f, separationDistance);
            float3 offset = position - otherPosition;
            float distanceSquared = math.lengthsq(offset);
            float radiusSquared = radius * radius;

            if (distanceSquared <= Epsilon * Epsilon || distanceSquared > radiusSquared)
                return float3.zero;

            float distance = math.sqrt(distanceSquared);
            float falloff = 1f - distance / math.max(radius, Epsilon);
            return math.normalizesafe(offset, float3.zero) * (falloff / distance);
        }

        /// <summary>
        /// 计算指向邻居平均位置的聚拢向量。
        /// </summary>
        public static float3 CalculateCohesion(float3 position, float3 averagePosition)
        {
            return averagePosition - position;
        }

        /// <summary>
        /// 计算当前速度向邻居平均速度靠拢的对齐向量。
        /// </summary>
        public static float3 CalculateAlignment(float3 velocity, float3 averageVelocity)
        {
            return averageVelocity - velocity;
        }

        /// <summary>
        /// 计算把实体推回游泳区域的边界力。
        /// 边界力会在边界预警距离内逐渐生效，越界后继续指向区域内部。
        /// </summary>
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

        /// <summary>
        /// 以最大加速度积分速度，并将最终速度限制在配置范围内。
        /// </summary>
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
                    return float3.zero;

                return new float3(0f, 0f, minimumSpeed);
            }

            return nextVelocity / speed * math.clamp(speed, minimumSpeed, maximumSpeed);
        }

        /// <summary>
        /// 将向量长度限制在指定最大值内。
        /// </summary>
        public static float3 ClampMagnitude(float3 value, float maximumLength)
        {
            float maxLength = math.max(0f, maximumLength);
            float lengthSquared = math.lengthsq(value);
            float maxLengthSquared = maxLength * maxLength;

            if (lengthSquared <= maxLengthSquared || lengthSquared <= Epsilon * Epsilon)
                return value;

            return value * (maxLength / math.sqrt(lengthSquared));
        }

        /// <summary>
        /// 根据世界种子和 Chunk 首实体索引生成稳定且非零的 Chunk 随机种子。
        /// </summary>
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

        private static float CalculateAxisBoundaryForce(
            float coordinate,
            float minimum,
            float maximum,
            float margin)
        {
            float halfExtent = math.max((maximum - minimum) * 0.5f, Epsilon);
            float safeMargin = math.min(math.max(margin, Epsilon), halfExtent);

            if (coordinate < minimum)
                return 1f + (minimum - coordinate) / safeMargin;
            if (coordinate > maximum)
                return -1f - (coordinate - maximum) / safeMargin;
            if (coordinate < minimum + safeMargin)
                return (minimum + safeMargin - coordinate) / safeMargin;
            if (coordinate > maximum - safeMargin)
                return -(coordinate - (maximum - safeMargin)) / safeMargin;

            return 0f;
        }
    }
}
