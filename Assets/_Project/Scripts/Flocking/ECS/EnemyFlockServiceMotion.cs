using System.Collections.Generic;
using GeometricMotion;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace FlockingSystem.ECS
{
    /// <summary>缓存行为位移，并把运动模式和命令安全写入 ECS 代理。</summary>
    public sealed partial class EnemyFlockRuntimeService
    {
        private readonly Dictionary<EnemyFlockBridge, float3> _pendingAgentDisplacements = new();
        private readonly Dictionary<EnemyFlockBridge, DirectedMotion> _pendingDirectedMotions = new();
        private readonly Dictionary<EnemyFlockBridge, GeometricMotionSignal> _pendingMotionSignals = new();

        /// <summary>待提交的定向位移和旋转。</summary>
        private readonly struct DirectedMotion
        {
            public readonly float3 Displacement;
            public readonly quaternion Rotation;

            /// <summary>保存单帧定向运动输入。</summary>
            /// <param name="displacement">世界坐标位移。</param>
            /// <param name="rotation">世界坐标旋转。</param>
            public DirectedMotion(float3 displacement, quaternion rotation)
            {
                Displacement = displacement;
                Rotation = rotation;
            }
        }

        /// <summary>检查是否有等待写入的运动命令。</summary>
        private bool HasPendingMotion =>
            _pendingAgentDisplacements.Count > 0 ||
            _pendingDirectedMotions.Count > 0 ||
            _pendingMotionSignals.Count > 0;

        /// <summary>更新代理运动模式，并清理模式切换时失效的暂存姿态。</summary>
        /// <param name="bridge">目标敌人的 ECS 桥接。</param>
        /// <param name="mode">新的运动所有权模式。</param>
        internal void UpdateAgentMotionMode(
            EnemyFlockBridge bridge,
            EnemyFlockMotionMode mode)
        {
            if (!CanUpdateAgent(bridge))
            {
                return;
            }

            CompleteJobsBeforeStructuralChange();
            EnemyFlockAgent agent = _entityManager.GetComponentData<EnemyFlockAgent>(bridge.Entity);
            if (mode == EnemyFlockMotionMode.Flocking)
            {
                agent.ExternalDisplacement += _pendingDirectedMotions.TryGetValue(
                    bridge,
                    out DirectedMotion pendingMotion)
                    ? pendingMotion.Displacement
                    : agent.DirectedDisplacement;
                _pendingDirectedMotions.Remove(bridge);
            }

            if (mode == EnemyFlockMotionMode.Frozen)
            {
                agent.ExternalImpulse = float3.zero;
                agent.ExternalDisplacement = float3.zero;
                _pendingAgentDisplacements.Remove(bridge);
                _pendingDirectedMotions.Remove(bridge);
                _pendingMotionSignals.Remove(bridge);
            }

            agent.MotionMode = mode;
            agent.DirectedDisplacement = float3.zero;
            agent.DirectedRotation = _entityManager
                .GetComponentData<LocalTransform>(bridge.Entity)
                .Rotation;
            _entityManager.SetComponentData(bridge.Entity, agent);
        }

        /// <summary>合并一次性位移，等待服务帧阶段写入代理。</summary>
        /// <param name="bridge">目标敌人的 ECS 桥接。</param>
        /// <param name="displacement">世界坐标位移。</param>
        internal void QueueAgentDisplacement(EnemyFlockBridge bridge, float3 displacement)
        {
            if (!CanUpdateAgent(bridge) || bridge.MotionMode == EnemyFlockMotionMode.Frozen)
            {
                return;
            }

            if (_pendingAgentDisplacements.TryGetValue(bridge, out float3 pending))
            {
                displacement += pending;
            }

            _pendingAgentDisplacements[bridge] = displacement;
        }

        /// <summary>保存定向状态的最新位移和旋转命令。</summary>
        /// <param name="bridge">目标敌人的 ECS 桥接。</param>
        /// <param name="displacement">本次模拟使用的世界位移。</param>
        /// <param name="rotation">本次模拟使用的世界旋转。</param>
        internal void UpdateAgentDirectedMotion(
            EnemyFlockBridge bridge,
            float3 displacement,
            quaternion rotation)
        {
            if (!CanUpdateAgent(bridge) || bridge.MotionMode != EnemyFlockMotionMode.Directed)
            {
                return;
            }

            _pendingDirectedMotions[bridge] = new DirectedMotion(displacement, rotation);
        }

        /// <summary>合并一个待 ECS 表现系统消费的几何运动信号。</summary>
        /// <param name="bridge">目标敌人的 ECS 桥接。</param>
        /// <param name="signal">要排队的几何运动信号。</param>
        internal void QueueMotionSignal(
            EnemyFlockBridge bridge,
            GeometricMotionSignal signal)
        {
            if (!CanUpdateAgent(bridge) || signal.Type == GeometricMotionSignalType.None)
            {
                return;
            }

            if (_pendingMotionSignals.TryGetValue(bridge, out GeometricMotionSignal pending))
            {
                signal = GeometricMotionMath.MergeSignals(pending, signal);
            }

            _pendingMotionSignals[bridge] = signal;
        }

        /// <summary>在已完成 ECS Job 后批量写入等待中的运动命令。</summary>
        private void FlushPendingMotion()
        {
            foreach (KeyValuePair<EnemyFlockBridge, float3> pending in _pendingAgentDisplacements)
            {
                EnemyFlockBridge bridge = pending.Key;
                if (!CanUpdateAgent(bridge))
                {
                    continue;
                }

                EnemyFlockAgent agent = _entityManager.GetComponentData<EnemyFlockAgent>(bridge.Entity);
                if (agent.MotionMode == EnemyFlockMotionMode.Frozen)
                {
                    continue;
                }

                agent.ExternalDisplacement += pending.Value;
                _entityManager.SetComponentData(bridge.Entity, agent);
            }

            foreach (KeyValuePair<EnemyFlockBridge, DirectedMotion> pending in _pendingDirectedMotions)
            {
                EnemyFlockBridge bridge = pending.Key;
                if (!CanUpdateAgent(bridge))
                {
                    continue;
                }

                EnemyFlockAgent agent = _entityManager.GetComponentData<EnemyFlockAgent>(bridge.Entity);
                if (agent.MotionMode != EnemyFlockMotionMode.Directed)
                {
                    continue;
                }

                agent.DirectedDisplacement = pending.Value.Displacement;
                agent.DirectedRotation = pending.Value.Rotation;
                _entityManager.SetComponentData(bridge.Entity, agent);
            }

            foreach (KeyValuePair<EnemyFlockBridge, GeometricMotionSignal> pending in _pendingMotionSignals)
            {
                EnemyFlockBridge bridge = pending.Key;
                if (!CanUpdateAgent(bridge) ||
                    !_entityManager.HasComponent<GeometricMotionSignal>(bridge.Entity))
                {
                    continue;
                }

                GeometricMotionSignal current =
                    _entityManager.GetComponentData<GeometricMotionSignal>(bridge.Entity);
                _entityManager.SetComponentData(
                    bridge.Entity,
                    GeometricMotionMath.MergeSignals(current, pending.Value));
            }

            ClearPendingMotion();
        }

        /// <summary>释放运动命令缓存。</summary>
        private void ClearPendingMotion()
        {
            _pendingAgentDisplacements.Clear();
            _pendingDirectedMotions.Clear();
            _pendingMotionSignals.Clear();
        }

        /// <summary>丢弃已解绑代理尚未提交的运动命令。</summary>
        /// <param name="bridge">已解绑敌人的 ECS 桥接。</param>
        private void ForgetPendingMotion(EnemyFlockBridge bridge)
        {
            _pendingAgentDisplacements.Remove(bridge);
            _pendingDirectedMotions.Remove(bridge);
            _pendingMotionSignals.Remove(bridge);
        }
    }
}
