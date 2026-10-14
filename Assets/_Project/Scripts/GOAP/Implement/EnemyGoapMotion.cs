using FlockingSystem.ECS;
using UnityEngine;

namespace EnemySystem.GOAP
{
    /// <summary>把 GOAP 移动意图转换为 Flocking Bridge 定向命令。</summary>
    internal static class EnemyGoapMotion
    {
        private const float MinimumDirectionSquared = 0.0001f;

        /// <summary>向目标移动一帧，并将朝向提交给 ECS。</summary>
        /// <param name="enemy">移动敌人。</param>
        /// <param name="bridge">ECS 移动桥接。</param>
        /// <param name="targetPosition">目标位置。</param>
        /// <param name="stoppingDistance">停止距离。</param>
        /// <param name="baseSpeed">基础移动速度。</param>
        /// <param name="speedMultiplier">当前阶段速度倍率。</param>
        /// <param name="turnSpeed">转向速度。</param>
        /// <returns>已经进入停止距离时返回 true。</returns>
        internal static bool MoveTowards(
            Enemy enemy,
            EnemyFlockBridge bridge,
            Vector3 targetPosition,
            float stoppingDistance,
            float baseSpeed,
            float speedMultiplier,
            float turnSpeed)
        {
            Vector3 offset = targetPosition - enemy.transform.position;
            float distance = offset.magnitude;
            if (distance <= stoppingDistance)
            {
                bridge.SetMotionMode(EnemyFlockMotionMode.Directed);
                bridge.SetDirectedMotion(Vector3.zero, enemy.transform.rotation);
                return true;
            }

            if (offset.sqrMagnitude <= MinimumDirectionSquared)
            {
                return false;
            }

            Quaternion targetRotation = Quaternion.LookRotation(offset, Vector3.up);
            Quaternion rotation = Quaternion.RotateTowards(
                enemy.transform.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime);
            float speed = Mathf.Max(0f, baseSpeed) *
                          enemy.GetMovementSpeedMultiplier() *
                          Mathf.Max(0f, speedMultiplier);
            float displacement = Mathf.Min(speed * Time.deltaTime, distance - stoppingDistance);

            bridge.SetMotionMode(EnemyFlockMotionMode.Directed);
            bridge.SetDirectedMotion(offset.normalized * displacement, rotation);
            return false;
        }

        /// <summary>恢复群游运动所有权。</summary>
        internal static void ResumeFlocking(EnemyFlockBridge bridge)
        {
            bridge.SetMotionMode(EnemyFlockMotionMode.Flocking);
        }

        /// <summary>停止定向移动并保留当前姿态。</summary>
        internal static void Hold(Enemy enemy, EnemyFlockBridge bridge)
        {
            bridge.SetMotionMode(EnemyFlockMotionMode.Directed);
            bridge.SetDirectedMotion(Vector3.zero, enemy.transform.rotation);
        }
    }
}
