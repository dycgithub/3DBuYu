using UnityEngine;

namespace CombatSystem
{
    /// <summary>计算批次投射物的发射参数，并约束起点和步进不越过射程球面。</summary>
    internal static class ProjectileLaunchPattern
    {
        private const float MinimumCenterSeparation = 0.01f;
        private const float WorldRangeBoundaryTolerance = 0.0001f;

        /// <summary>计算批次中一颗投射物相对中心线的横向起点。</summary>
        /// <param name="origin">发射器或分裂命中点的基准位置。</param>
        /// <param name="direction">批次的中心发射方向。</param>
        /// <param name="projectileCount">本批投射物总数。</param>
        /// <param name="projectileIndex">当前投射物在批次中的序号。</param>
        /// <param name="minimumSpacing">相邻投射物的最小中心间距。</param>
        /// <returns>对称排列后的投射物起点。</returns>
        internal static Vector3 GetOriginPosition(
            Vector3 origin,
            Vector3 direction,
            int projectileCount,
            int projectileIndex,
            float minimumSpacing)
        {
            if (projectileCount <= 1 || minimumSpacing <= 0f)
            {
                return origin;
            }

            Vector3 normalizedDirection = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector3.forward;
            Vector3 lateralDirection = Vector3.Cross(Vector3.up, normalizedDirection);
            if (lateralDirection.sqrMagnitude <= 0.0001f)
            {
                lateralDirection = Vector3.Cross(Vector3.right, normalizedDirection);
            }

            float centeredIndex = projectileIndex - (projectileCount - 1f) * 0.5f;
            return origin + lateralDirection.normalized * centeredIndex * minimumSpacing;
        }

        /// <summary>按弹体视觉宽度和碰撞直径取批次最小间距。</summary>
        /// <param name="profile">投射物视觉配置。</param>
        /// <param name="collisionRadius">最终碰撞扫掠半径。</param>
        /// <returns>保证相邻弹体不重叠的中心距离。</returns>
        internal static float GetMinimumSpacing(
            ProjectileProfileSO profile,
            float collisionRadius)
        {
            float visualWidth = Mathf.Max(
                0f,
                profile != null &&
                !(profile.TrajectoryDefinition is LaserTrajectoryDefinitionSO) &&
                profile.VisualDefinition != null
                    ? profile.VisualDefinition.ProjectileWidth
                    : 0f);
            float collisionDiameter = Mathf.Max(0f, collisionRadius) * 2f;
            return Mathf.Max(MinimumCenterSeparation, visualWidth, collisionDiameter);
        }

        /// <summary>按批次序号生成对称扇形方向。</summary>
        internal static Vector3 GetDirection(
            Vector3 direction,
            int projectileCount,
            int projectileIndex,
            float maximumAngleDegrees)
        {
            Vector3 normalizedDirection = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector3.forward;
            if (projectileCount <= 1 || maximumAngleDegrees <= 0f)
            {
                return normalizedDirection;
            }

            float centeredIndex = projectileIndex / (projectileCount - 1f) - 0.5f;
            float angle = centeredIndex * 2f * Mathf.Clamp(maximumAngleDegrees, 0f, 180f);
            Vector3 rotationAxis = Mathf.Abs(Vector3.Dot(normalizedDirection, Vector3.up)) > 0.99f
                ? Vector3.right
                : Vector3.up;
            return Quaternion.AngleAxis(angle, rotationAxis) * normalizedDirection;
        }

        /// <summary>围绕瞄准方向在限定角度内生成一个随机方向。</summary>
        internal static Vector3 GetRandomDirection(Vector3 direction, float maximumAngleDegrees)
        {
            Vector3 normalizedDirection = direction.sqrMagnitude > 0.0001f
                ? direction.normalized
                : Vector3.forward;
            float maximumAngle = Mathf.Max(0f, maximumAngleDegrees);
            float angle = Random.Range(-maximumAngle, maximumAngle);
            Vector3 rotationAxis = Mathf.Abs(Vector3.Dot(normalizedDirection, Vector3.up)) > 0.99f
                ? Vector3.right
                : Vector3.up;
            return (Quaternion.AngleAxis(angle, rotationAxis) * normalizedDirection).normalized;
        }

        /// <summary>对单颗投射物应用一次性、对称的速度随机变化。</summary>
        internal static float GetSpeed(float speed, float variance)
        {
            float clampedVariance = Mathf.Clamp(variance, 0f, 0.5f);
            if (clampedVariance <= 0f)
            {
                return speed;
            }

            return speed * UnityEngine.Random.Range(1f - clampedVariance, 1f + clampedVariance);
        }

        /// <summary>限制新投射物起点，使其位于射程球面内。</summary>
        internal static Vector3 ClampToWorldRange(
            Vector3 position,
            Vector3 rangeOrigin,
            float rangeRadius)
        {
            float radius = Mathf.Max(0f, rangeRadius);
            Vector3 offset = position - rangeOrigin;
            if (offset.sqrMagnitude <= radius * radius)
            {
                return position;
            }

            return rangeOrigin + offset.normalized * radius;
        }

        /// <summary>计算沿给定方向到达射程球面的最大步进距离。</summary>
        internal static float GetWorldRangeStepDistance(
            Vector3 position,
            Vector3 direction,
            Vector3 rangeOrigin,
            float rangeRadius)
        {
            float radius = Mathf.Max(0f, rangeRadius);
            if (radius <= 0f || direction.sqrMagnitude <= 0.0001f)
            {
                return 0f;
            }

            Vector3 offset = position - rangeOrigin;
            float radiusSquared = radius * radius;
            float offsetSquared = offset.sqrMagnitude;
            if (offsetSquared > radiusSquared + WorldRangeBoundaryTolerance)
            {
                return 0f;
            }

            Vector3 normalizedDirection = direction.normalized;
            float directionProjection = Vector3.Dot(offset, normalizedDirection);
            float discriminant = directionProjection * directionProjection +
                radiusSquared - offsetSquared;
            return Mathf.Max(0f, -directionProjection + Mathf.Sqrt(Mathf.Max(0f, discriminant)));
        }

        /// <summary>判断位置是否已经到达射程球面。</summary>
        internal static bool IsAtWorldRangeBoundary(
            Vector3 position,
            Vector3 rangeOrigin,
            float rangeRadius)
        {
            float radius = Mathf.Max(0f, rangeRadius);
            float boundaryRadius = Mathf.Max(0f, radius - WorldRangeBoundaryTolerance);
            return (position - rangeOrigin).sqrMagnitude >= boundaryRadius * boundaryRadius;
        }

        /// <summary>补齐范围球心、应用发射偏移并限制投射物起点。</summary>
        internal static void PrepareWorldRange(ref ProjectileData projectileData, Vector3 direction)
        {
            if (!projectileData.HasWorldRangeOrigin)
            {
                projectileData.RangeOriginPosition = projectileData.OriginPosition;
                projectileData.HasWorldRangeOrigin = true;
            }

            projectileData.OriginPosition += direction * Mathf.Max(
                0f,
                projectileData.ProjectileSpawnOffset);
            projectileData.ProjectileSpawnOffset = 0f;
            projectileData.OriginPosition = ClampToWorldRange(
                projectileData.OriginPosition,
                projectileData.RangeOriginPosition,
                projectileData.MaximumTravelDistance);
        }

        /// <summary>限制 Hitscan 射线长度，确保终点不越过射程球面。</summary>
        internal static void LimitRayToWorldRange(ref ProjectileData projectileData, Vector3 direction)
        {
            projectileData.MaximumTravelDistance = Mathf.Min(
                projectileData.MaximumTravelDistance,
                GetWorldRangeStepDistance(
                    projectileData.OriginPosition,
                    direction,
                    projectileData.RangeOriginPosition,
                    projectileData.MaximumTravelDistance));
        }
    }
}
