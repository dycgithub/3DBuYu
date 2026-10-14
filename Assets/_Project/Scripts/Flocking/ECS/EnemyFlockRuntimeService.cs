using System;
using System.Collections.Generic;
using EnemySystem;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Rendering;
using UnityEngine;
using VContainer.Unity;

namespace FlockingSystem.ECS
{
    /// <summary>
    /// 管理 ECS 敌人的实体、槽位、运动命令和托管 Transform 桥接。
    /// <para>
    /// 本服务只处理实体生命周期、托管命令和表现资源；群游计算由
    /// EnemyFlockSimulationSystem 执行，最终 Transform 写入由 EnemyFlockCommitSystem 执行。
    /// </para>
    /// </summary>
    public sealed partial class EnemyFlockRuntimeService : IDisposable, ITickable
    {
        private const float InitialSpeedSeedStep = 17.13f;

        private static readonly ProfilerMarker TickMarker =
            new("3DBuYu/VContainer/EnemyFlockRuntimeService.Tick");
        private static readonly ProfilerMarker CompleteJobsMarker =
            new("3DBuYu/EnemyFlockRuntimeService.CompleteJobsBeforeStructuralChange");

        /// <summary>场景级静态配置；服务不修改资产内容。</summary>
        private readonly EnemyFlockSettingsSO _flockSettings;

        /// <summary>按群游槽位反查托管桥接，供交换删除时修正 Transform 索引。</summary>
        private readonly Dictionary<int, EnemyFlockBridge> _bridgesBySlot = new();

        /// <summary>已释放、可复用的群游槽位。</summary>
        private readonly Queue<int> _freeSlots = new();

        /// <summary>等待 ITickable 批量写入的速度倍率。</summary>
        private readonly Dictionary<EnemyFlockBridge, float> _pendingSpeedMultipliers = new();

        /// <summary>等待安全 ECS 阶段写入的实例视觉状态。</summary>
        private readonly Dictionary<EnemyFlockBridge, EnemyFlockVisualState> _pendingVisualStates = new();

        private World _world;
        private EntityManager _entityManager;
        private EnemyFlockCommitSystem _commitSystem;
        private Entity _prototype;
        private Entity _configEntity;
        private float _maximumNeighbourDistance;
        private float _maximumSeparationDistance;
        private int _nextSlot;

        /// <summary>静态世界配置已经写入当前 ECS World 的标记。</summary>
        private bool _worldConfigInitialized;

        private bool _disposed;
        private bool _warnedMissingWorld;
        private bool _warnedShaderFallback;

        /// <summary>
        /// 使用场景级 Flocking 配置创建运行时服务。
        /// </summary>
        /// <param name="settings">由 GameLoopLifetimeScope 注册的静态配置资产。</param>
        public EnemyFlockRuntimeService(EnemyFlockSettingsSO settings)
        {
            _flockSettings = settings;
        }

        /// <summary>
        /// 批量提交速度、视觉和运动命令，在 ECS 安全阶段统一写入。
        /// </summary>
        public void Tick()
        {
            using var profilerScope = TickMarker.Auto();
            if (_pendingSpeedMultipliers.Count == 0 &&
                _pendingVisualStates.Count == 0 &&
                !HasPendingMotion)
            {
                return;
            }

            if (_disposed || _world == null || !_world.IsCreated)
            {
                _pendingSpeedMultipliers.Clear();
                _pendingVisualStates.Clear();
                ClearPendingMotion();
                return;
            }

            CompleteJobsBeforeStructuralChange();
            foreach (KeyValuePair<EnemyFlockBridge, float> pending in _pendingSpeedMultipliers)
            {
                EnemyFlockBridge bridge = pending.Key;
                if (!CanUpdateAgent(bridge))
                {
                    continue;
                }

                EnemyFlockAgent agent = _entityManager.GetComponentData<EnemyFlockAgent>(bridge.Entity);
                agent.SpeedMultiplier = pending.Value;
                _entityManager.SetComponentData(bridge.Entity, agent);
            }

            foreach (KeyValuePair<EnemyFlockBridge, EnemyFlockVisualState> pending in _pendingVisualStates)
            {
                ApplyVisualState(pending.Key, pending.Value);
            }

            FlushPendingMotion();
            _pendingSpeedMultipliers.Clear();
            _pendingVisualStates.Clear();
        }

        /// <summary>
        /// 获取或复用一个 ECS Flocking 实体，并同步其初始姿态和行为配置。
        /// </summary>
        /// <returns>绑定成功返回 true；配置、World、渲染资源或容量不足时返回 false。</returns>
        public bool TryAcquireAgent(
            EnemyFlockBridge bridge,
            GameObject sourcePrefab,
            EnemyType enemyType,
            float speedMultiplier,
            Vector3 position,
            Quaternion rotation)
        {
            if (_disposed || _flockSettings == null || bridge == null ||
                sourcePrefab == null || !TryInitializeEcsWorld())
            {
                return false;
            }

            if (bridge.Runtime != null && bridge.Runtime != this)
            {
                return false;
            }

            CompleteJobsBeforeStructuralChange();
            EnemyFlockProfile profile = _flockSettings.GetProfileForEnemyType(enemyType, speedMultiplier);
            bool worldConfigChanged = !_worldConfigInitialized
                || _configEntity == Entity.Null
                || !_entityManager.Exists(_configEntity)
                || profile.NeighbourDistance > _maximumNeighbourDistance
                || profile.SeparationDistance > _maximumSeparationDistance;
            if (worldConfigChanged)
            {
                _maximumNeighbourDistance = Mathf.Max(
                    _maximumNeighbourDistance,
                    profile.NeighbourDistance);
                _maximumSeparationDistance = Mathf.Max(
                    _maximumSeparationDistance,
                    profile.SeparationDistance);
                EnsureWorldConfigEntity();
                UpdateWorldConfig();
                _worldConfigInitialized = true;
            }

            if (!TryGetVisualDefinition(sourcePrefab, profile.VisualIndex, out EnemyFlockVisual visual))
            {
                return false;
            }

            if (_prototype == Entity.Null || !_entityManager.Exists(_prototype))
            {
                _prototype = CreateAgentPrototype(visual);
            }

            bool reuse = bridge.IsEcsControlled && _entityManager.Exists(bridge.Entity);
            if (!reuse && bridge.IsEcsControlled)
            {
                UnbindAgent(bridge);
            }

            if (!reuse)
            {
                int slot = AllocateSlot();
                if (slot < 0)
                {
                    return false;
                }

                Entity entity = _entityManager.Instantiate(_prototype);
                int transformIndex = _commitSystem.RegisterAgentTransform(bridge.transform, slot);
                if (transformIndex < 0)
                {
                    _entityManager.DestroyEntity(entity);
                    _freeSlots.Enqueue(slot);
                    return false;
                }

                bridge.AttachEcsAgent(
                    this,
                    entity,
                    slot,
                    transformIndex,
                    profile.SpeedMultiplier,
                    visual.BaseColor);
                _bridgesBySlot[slot] = bridge;
            }

            bridge.CacheBaseColor(visual.BaseColor);
            bridge.CacheVisualColors(visual.CrackColor, visual.EmissionColor);
            SetEntityFlockData(bridge, profile, visual, position, rotation);
            bridge.CacheSpeedMultiplier(profile.SpeedMultiplier);
            bridge.SetEcsPresentationEnabled(true);
            _commitSystem.SetAgentTransformActive(bridge.TransformArrayIndex, true);
            return true;
        }

        /// <summary>
        /// 释放实体和 slot，避免闲置预制体池持续占用全局容量。
        /// </summary>
        public bool ReleaseAgent(
            EnemyFlockBridge bridge,
            bool restoreSourceRenderer = true)
        {
            if (_disposed
                || _world == null
                || !_world.IsCreated
                || bridge == null
                || bridge.Runtime != this)
            {
                return false;
            }

            if (!_entityManager.Exists(bridge.Entity))
            {
                UnbindAgent(bridge, restoreSourceRenderer);
                return false;
            }

            UnbindAgent(bridge, restoreSourceRenderer);
            return true;
        }

        /// <summary>缓存代理速度倍率，等待本帧 ITickable 阶段统一写入 ECS。</summary>
        /// <param name="bridge">目标敌人的 ECS 桥接。</param>
        /// <param name="multiplier">相对基础速度的非负倍率。</param>
        internal void UpdateAgentSpeedMultiplier(EnemyFlockBridge bridge, float multiplier)
        {
            if (!CanUpdateAgent(bridge))
            {
                return;
            }

            _pendingSpeedMultipliers[bridge] = Mathf.Max(0f, multiplier);
        }

        /// <summary>缓存单个敌人的实例视觉状态，等待 ECS 安全阶段统一写入。</summary>
        internal void UpdateAgentVisualState(
            EnemyFlockBridge bridge,
            EnemyFlockVisualState state)
        {
            if (!CanUpdateAgent(bridge))
            {
                return;
            }

            _pendingVisualStates[bridge] = state;
        }

        /// <summary>同步托管敌人重新注册后的空间查询槽位。</summary>
        internal void UpdateAgentSpatialSlot(EnemyFlockBridge bridge, int spatialSlot)
        {
            if (!CanUpdateAgent(bridge) ||
                !_entityManager.HasComponent<EnemyFlockBridgeIndex>(bridge.Entity))
            {
                return;
            }

            CompleteJobsBeforeStructuralChange();
            EnemyFlockBridgeIndex index =
                _entityManager.GetComponentData<EnemyFlockBridgeIndex>(bridge.Entity);
            if (index.SpatialSlot == spatialSlot)
            {
                return;
            }

            index.SpatialSlot = spatialSlot;
            _entityManager.SetComponentData(bridge.Entity, index);
        }

        /// <summary>累加一次待群游模拟消费的外部速度冲量。</summary>
        /// <param name="bridge">目标敌人的 ECS 桥接。</param>
        /// <param name="impulse">单位为世界单位/秒的冲量。</param>
        internal void QueueAgentExternalImpulse(EnemyFlockBridge bridge, float3 impulse)
        {
            if (!CanUpdateAgent(bridge))
            {
                return;
            }

            CompleteJobsBeforeStructuralChange();
            EnemyFlockAgent agent = _entityManager.GetComponentData<EnemyFlockAgent>(bridge.Entity);
            agent.ExternalImpulse += impulse;
            _entityManager.SetComponentData(bridge.Entity, agent);
        }

        /// <summary>更新单个 ECS 敌人的受击或死亡颜色。</summary>
        /// <param name="bridge">目标敌人与 ECS 实体的桥接。</param>
        /// <param name="color">线性空间颜色。</param>
        internal void SetAgentPresentationColor(EnemyFlockBridge bridge, float4 color)
        {
            if (!CanUpdatePresentation(bridge))
            {
                return;
            }

            _entityManager.SetComponentData(
                bridge.Entity,
                new URPMaterialPropertyBaseColor { Value = color });
        }

        /// <summary>恢复单个 ECS 敌人的基础颜色。</summary>
        /// <param name="bridge">目标敌人与 ECS 实体的桥接。</param>
        /// <param name="color">绑定时保存的线性空间基础颜色。</param>
        internal void ResetAgentPresentationColor(EnemyFlockBridge bridge, float4 color)
        {
            SetAgentPresentationColor(bridge, color);
        }

        /// <summary>
        /// 销毁 ECS 代理、释放群游槽位，并修正交换删除移动的托管 Transform 索引。
        /// </summary>
        /// <param name="bridge">要解除绑定的桥接组件。</param>
        /// <param name="restoreSourceRenderer">是否恢复源预制体 Renderer。</param>
        internal void UnbindAgent(
            EnemyFlockBridge bridge,
            bool restoreSourceRenderer = true)
        {
            if (bridge == null || bridge.Runtime != this)
            {
                return;
            }

            _pendingSpeedMultipliers.Remove(bridge);
            _pendingVisualStates.Remove(bridge);
            ForgetPendingMotion(bridge);

            if (_disposed)
            {
                bridge.ClearEcsBinding(restoreSourceRenderer);
                return;
            }

            if (_world == null || !_world.IsCreated)
            {
                _bridgesBySlot.Remove(bridge.Slot);
                bridge.ClearEcsBinding(restoreSourceRenderer);
                return;
            }

            CompleteJobsBeforeStructuralChange();
            int slot = bridge.Slot;
            if (_entityManager.Exists(bridge.Entity))
            {
                _entityManager.DestroyEntity(bridge.Entity);
            }

            int movedSlot = _commitSystem.UnregisterAgentTransform(bridge.TransformArrayIndex);
            if (movedSlot >= 0 && _bridgesBySlot.TryGetValue(movedSlot, out EnemyFlockBridge movedBridge))
            {
                movedBridge.TransformArrayIndex = bridge.TransformArrayIndex;
            }

            _bridgesBySlot.Remove(slot);
            _freeSlots.Enqueue(slot);
            bridge.ClearEcsBinding(restoreSourceRenderer);
        }

        /// <summary>
        /// 完成 Job、销毁 ECS 实体和原型，并释放运行时渲染资源。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_world != null && _world.IsCreated)
            {
                CompleteJobsBeforeStructuralChange();
                while (_bridgesBySlot.Count > 0)
                {
                    UnbindAgent(GetAnyBridge());
                }

                if (_prototype != Entity.Null && _entityManager.Exists(_prototype))
                {
                    _entityManager.DestroyEntity(_prototype);
                }

                if (_configEntity != Entity.Null && _entityManager.Exists(_configEntity))
                {
                    _entityManager.DestroyEntity(_configEntity);
                }
            }

            DisposeVisualResources();
            _pendingSpeedMultipliers.Clear();
            _pendingVisualStates.Clear();
            ClearPendingMotion();
            _disposed = true;
        }

        /// <summary>优先复用已释放槽位，否则分配下一个连续槽位。</summary>
        private int AllocateSlot()
        {
            if (_freeSlots.Count > 0)
            {
                return _freeSlots.Dequeue();
            }

            return _nextSlot < _flockSettings.MaximumAgents ? _nextSlot++ : -1;
        }

        /// <summary>取得任意仍绑定的桥接，供 Dispose 循环解除全部代理。</summary>
        private EnemyFlockBridge GetAnyBridge()
        {
            foreach (var bridge in _bridgesBySlot.Values)
            {
                return bridge;
            }

            return null;
        }

        /// <summary>结构化修改或主线程读取前等待所有 ECS Job 完成。</summary>
        private void CompleteJobsBeforeStructuralChange()
        {
            using var profilerScope = CompleteJobsMarker.Auto();
            if (_world != null && _world.IsCreated)
            {
                _entityManager.CompleteAllTrackedJobs();
            }
        }

        /// <summary>检查桥接仍拥有可写入的活动 ECS 代理。</summary>
        private bool CanUpdateAgent(EnemyFlockBridge bridge)
        {
            return !_disposed
                && _world != null
                && _world.IsCreated
                && bridge != null
                && bridge.Runtime == this
                && _entityManager.Exists(bridge.Entity)
                && _entityManager.HasComponent<EnemyFlockAgent>(bridge.Entity);
        }

        /// <summary>检查代理存在材质颜色组件且可以安全更新表现。</summary>
        private bool CanUpdatePresentation(EnemyFlockBridge bridge)
        {
            return CanUpdateAgent(bridge)
                && _entityManager.HasComponent<URPMaterialPropertyBaseColor>(bridge.Entity);
        }

        /// <summary>把缓存的视觉状态写入 Entities Graphics 实例属性。</summary>
        private void ApplyVisualState(
            EnemyFlockBridge bridge,
            EnemyFlockVisualState state)
        {
            if (!CanUpdateAgent(bridge))
            {
                return;
            }

            SetComponentIfPresent(
                bridge.Entity,
                new URPMaterialPropertyBaseColor { Value = state.BaseColor });
            SetComponentIfPresent(
                bridge.Entity,
                new EnemyFlockHealth01Property { Value = Mathf.Clamp01(state.Health01) });
            SetComponentIfPresent(
                bridge.Entity,
                new EnemyFlockHitFlashProperty { Value = Mathf.Clamp01(state.HitFlash) });
            SetComponentIfPresent(
                bridge.Entity,
                new EnemyFlockDeathProgressProperty { Value = Mathf.Clamp01(state.DeathProgress) });
            SetComponentIfPresent(
                bridge.Entity,
                new EnemyFlockCrackSeedProperty { Value = state.CrackSeed });
            SetComponentIfPresent(
                bridge.Entity,
                new EnemyFlockCrackColorProperty { Value = state.CrackColor });
            SetComponentIfPresent(
                bridge.Entity,
                new EnemyFlockEmissionColorProperty { Value = state.EmissionColor });
        }

        /// <summary>仅在原型包含指定材质属性时写入实例数据。</summary>
        private void SetComponentIfPresent<T>(Entity entity, T value)
            where T : unmanaged, IComponentData
        {
            if (_entityManager.HasComponent<T>(entity))
            {
                _entityManager.SetComponentData(entity, value);
            }
        }
    }
}
