using UnityEngine;

namespace EnemySystem.AreaEffect.Model
{
    /// <summary>保存一次由范围精英组件提交给 ECS 的定向运动结果。</summary>
    internal readonly struct EnemyAreaEffectMotion
    {
        internal EnemyAreaEffectMotion(Vector3 displacement, Quaternion rotation)
        {
            Displacement = displacement;
            Rotation = rotation;
        }

        internal Vector3 Displacement { get; }
        internal Quaternion Rotation { get; }
    }

    /// <summary>计算范围精英接近和撤退时的单帧位移与转向。</summary>
    internal static class EnemyAreaEffectMotionMath
    {
        private const float MinimumDirectionSquared = 0.0001f;

        /// <summary>计算进入范围效果距离时的单帧运动。</summary>
        /// <param name="position">敌人当前位置。</param>
        /// <param name="rotation">敌人当前朝向。</param>
        /// <param name="targetPosition">施法目标位置。</param>
        /// <param name="stoppingDistance">到达施法目标后的停止距离。</param>
        /// <param name="speed">已应用倍率和速度上限的移动速度。</param>
        /// <param name="turnSpeed">每秒最大转向角度。</param>
        /// <param name="deltaTime">本帧经过的秒数。</param>
        /// <returns>本帧定向位移和朝向。</returns>
        internal static EnemyAreaEffectMotion Approach(
            Vector3 position,
            Quaternion rotation,
            Vector3 targetPosition,
            float stoppingDistance,
            float speed,
            float turnSpeed,
            float deltaTime)
        {
            Vector3 offset = targetPosition - position;
            float distance = offset.magnitude;
            Vector3 direction = distance * distance > MinimumDirectionSquared
                ? offset / distance
                : Vector3.zero;
            float movementDistance = Mathf.Min(
                Mathf.Max(0f, speed) * Mathf.Max(0f, deltaTime),
                Mathf.Max(0f, distance - Mathf.Max(0f, stoppingDistance)));
            Quaternion targetRotation = direction == Vector3.zero
                ? rotation
                : Quaternion.LookRotation(direction, Vector3.up);
            Quaternion nextRotation = Quaternion.RotateTowards(
                rotation,
                targetRotation,
                Mathf.Max(0f, turnSpeed) * Mathf.Max(0f, deltaTime));

            return new EnemyAreaEffectMotion(direction * movementDistance, nextRotation);
        }

        /// <summary>计算沿玩家反方向撤退至生命对应安全距离的单帧运动。</summary>
        /// <param name="position">敌人当前位置。</param>
        /// <param name="rotation">敌人当前朝向。</param>
        /// <param name="playerPosition">玩家当前位置。</param>
        /// <param name="safeDistance">当前目标安全距离。</param>
        /// <param name="speed">已应用倍率和速度上限的移动速度。</param>
        /// <param name="turnSpeed">每秒最大转向角度。</param>
        /// <param name="deltaTime">本帧经过的秒数。</param>
        /// <returns>本帧定向位移和朝向。</returns>
        internal static EnemyAreaEffectMotion Retreat(
            Vector3 position,
            Quaternion rotation,
            Vector3 playerPosition,
            float safeDistance,
            float speed,
            float turnSpeed,
            float deltaTime)
        {
            Vector3 towardPlayer = playerPosition - position;
            float distance = towardPlayer.magnitude;
            Vector3 direction = towardPlayer.sqrMagnitude > MinimumDirectionSquared
                ? -towardPlayer.normalized
                : -(rotation * Vector3.forward);
            float movementDistance = Mathf.Min(
                Mathf.Max(0f, speed) * Mathf.Max(0f, deltaTime),
                Mathf.Max(0f, safeDistance - distance));
            Quaternion targetRotation = Quaternion.LookRotation(-direction, Vector3.up);
            Quaternion nextRotation = Quaternion.RotateTowards(
                rotation,
                targetRotation,
                Mathf.Max(0f, turnSpeed) * Mathf.Max(0f, deltaTime));

            return new EnemyAreaEffectMotion(direction * movementDistance, nextRotation);
        }
    }
}
