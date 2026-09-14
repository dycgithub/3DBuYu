using System;
using System.Collections.Generic;
using EnemySystem;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace FlockingSystem.ECS
{
    /// <summary>
    /// 管理 ECS 敌人的原型、slot、渲染资源和 GameObject Transform 桥接。
    /// 群游计算由 EnemyFlockSimulationSystem 执行，本服务不直接计算 Boids 规则。
    /// </summary>
    public sealed class EnemyFlockRuntimeService : IDisposable
    {
        private readonly EnemyFlockSettingsSO _flockSettings;
        private readonly Dictionary<int, EnemyFlockVisual> _visuals = new();
        private readonly Dictionary<int, EnemyFlockBridge> _bridgesBySlot = new();
        private readonly Queue<int> _freeSlots = new();
        private readonly List<Material> _runtimeMaterials = new();

        private World _world;
        private EntityManager _entityManager;
        private EnemyFlockCommitSystem _commitSystem;
        private Entity _prototype;
        private Entity _configEntity;
        private float _maximumNeighbourDistance;
        private float _maximumSeparationDistance;
        private int _nextSlot;
        private bool _disposed;
        private bool _warnedMissingWorld;
        private bool _warnedMissingGraphics;

        /// <summary>
        /// 使用场景级 Flocking 配置创建运行时服务。
        /// </summary>
        /// <param name="settings">由 GameLoopLifetimeScope 注册的静态配置资产。</param>
        public EnemyFlockRuntimeService(EnemyFlockSettingsSO settings)
        {
            _flockSettings = settings;
        }

        private struct EnemyFlockVisual
        {
            public BatchMaterialID MaterialId;
            public BatchMeshID MeshId;
            public PostTransformMatrix PostTransform;
            public float4 BaseColor;
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
            if (_disposed || _flockSettings == null || bridge == null || sourcePrefab == null || !TryInitializeEcsWorld())
            {
                return false;
            }

            if (bridge.Runtime != null && bridge.Runtime != this)
            {
                return false;
            }

            EnemyFlockProfile profile = _flockSettings.GetProfileForEnemyType(enemyType, speedMultiplier);

            _maximumNeighbourDistance = Mathf.Max(_maximumNeighbourDistance, profile.NeighbourDistance);
            _maximumSeparationDistance = Mathf.Max(_maximumSeparationDistance, profile.SeparationDistance);
            EnsureWorldConfigEntity();
            UpdateWorldConfig();

            if (!TryGetVisualDefinition(sourcePrefab, profile.VisualIndex, out EnemyFlockVisual visual))
            {
                return false;
            }

            CompleteJobsBeforeStructuralChange();
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
            SetEntityFlockData(bridge, profile, visual, position, rotation);
            bridge.CacheSpeedMultiplier(profile.SpeedMultiplier);
            bridge.SetEcsPresentationEnabled(true);
            _commitSystem.SetAgentTransformActive(bridge.TransformArrayIndex, true);
            return true;
        }

        /// <summary>
        /// 暂停一个已绑定实体，保留实体和 slot 供对象池复用。
        /// </summary>
        public bool ReleaseAgent(EnemyFlockBridge bridge)
        {
            if (_disposed
                || _world == null
                || !_world.IsCreated
                || bridge == null
                || bridge.Runtime != this
                || !_entityManager.Exists(bridge.Entity))
            {
                return false;
            }

            CompleteJobsBeforeStructuralChange();
            EnemyFlockAgent agent = _entityManager.GetComponentData<EnemyFlockAgent>(bridge.Entity);
            agent.ExternalImpulse = float3.zero;
            _entityManager.SetComponentData(bridge.Entity, agent);
            _entityManager.SetComponentEnabled<EnemyFlockActive>(bridge.Entity, false);
            _entityManager.SetComponentEnabled<MaterialMeshInfo>(bridge.Entity, false);
            _commitSystem.SetAgentTransformActive(bridge.TransformArrayIndex, false);
            return true;
        }

        internal void UpdateAgentSpeedMultiplier(EnemyFlockBridge bridge, float multiplier)
        {
            if (!CanUpdateAgent(bridge))
            {
                return;
            }

            CompleteJobsBeforeStructuralChange();
            EnemyFlockAgent agent = _entityManager.GetComponentData<EnemyFlockAgent>(bridge.Entity);
            agent.SpeedMultiplier = Mathf.Max(0f, multiplier);
            _entityManager.SetComponentData(bridge.Entity, agent);
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

        internal void UpdateAgentExternalMotion(EnemyFlockBridge bridge, bool enabled)
        {
            if (!CanUpdateAgent(bridge))
            {
                return;
            }

            CompleteJobsBeforeStructuralChange();
            EnemyFlockAgent agent = _entityManager.GetComponentData<EnemyFlockAgent>(bridge.Entity);
            agent.UseExternalMotion = enabled;
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

        internal void UnbindAgent(EnemyFlockBridge bridge)
        {
            if (bridge == null || bridge.Runtime != this)
            {
                return;
            }

            if (_disposed)
            {
                bridge.ClearEcsBinding();
                return;
            }

            if (_world == null || !_world.IsCreated)
            {
                _bridgesBySlot.Remove(bridge.Slot);
                bridge.ClearEcsBinding();
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
            bridge.ClearEcsBinding();
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

            if (_world != null && _world.IsCreated)
            {
                var graphics = _world.GetExistingSystemManaged<EntitiesGraphicsSystem>();
                foreach (var visual in _visuals.Values)
                {
                    graphics?.UnregisterMaterial(visual.MaterialId);
                    graphics?.UnregisterMesh(visual.MeshId);
                }
            }

            foreach (var material in _runtimeMaterials)
            {
                UnityEngine.Object.Destroy(material);
            }

            _visuals.Clear();
            _runtimeMaterials.Clear();
            _disposed = true;
        }

        private bool TryInitializeEcsWorld()
        {
            if (_flockSettings == null)
            {
                Debug.LogError("[EnemyFlock] EnemyFlockSettings 未配置，无法启动 ECS Flocking。");
                return false;
            }

            if (_world != null && _world.IsCreated)
            {
                return true;
            }

            _world = World.DefaultGameObjectInjectionWorld;
            if (_world == null || !_world.IsCreated)
            {
                if (!_warnedMissingWorld)
                {
                    Debug.LogError("[EnemyFlock] Default ECS World 不存在，无法启动 ECS Flocking。");
                    _warnedMissingWorld = true;
                }
                return false;
            }

            _entityManager = _world.EntityManager;
            _commitSystem = _world.GetOrCreateSystemManaged<EnemyFlockCommitSystem>();
            _commitSystem.ConfigureAgentCapacity(_flockSettings.MaximumAgents);
            return true;
        }

        private void EnsureWorldConfigEntity()
        {
            if (_configEntity != Entity.Null && _entityManager.Exists(_configEntity))
            {
                return;
            }

            using var query = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<EnemyFlockWorldConfig>());
            _configEntity = query.IsEmptyIgnoreFilter
                ? _entityManager.CreateEntity(typeof(EnemyFlockWorldConfig))
                : query.GetSingletonEntity();
        }

        private void UpdateWorldConfig()
        {
            Vector3 configuredLimits = _flockSettings.SwimLimits;
            float3 limits = new float3(configuredLimits.x, configuredLimits.y, configuredLimits.z);
            float3 extent = limits * 2f;
            float largestExtent = Mathf.Max(extent.x, Mathf.Max(extent.y, extent.z));
            float cellSize = _flockSettings.GridCellSize > 0f
                ? _flockSettings.GridCellSize
                : Mathf.Max(
                    Mathf.Max(_maximumNeighbourDistance, _maximumSeparationDistance),
                    largestExtent / _flockSettings.GridCellsPerAxis);
            int maximumDimension = _flockSettings.GridCellsPerAxis;
            int3 dimensions = new int3(
                Mathf.Clamp(Mathf.CeilToInt(extent.x / cellSize), 1, maximumDimension),
                Mathf.Clamp(Mathf.CeilToInt(extent.y / cellSize), 1, maximumDimension),
                Mathf.Clamp(Mathf.CeilToInt(extent.z / cellSize), 1, maximumDimension));
            Vector3 configuredCenter = _flockSettings.SwimCenter;

            _entityManager.SetComponentData(_configEntity, new EnemyFlockWorldConfig
            {
                SwimCenter = new float3(configuredCenter.x, configuredCenter.y, configuredCenter.z),
                SwimLimits = limits,
                GridOrigin = new float3(configuredCenter.x, configuredCenter.y, configuredCenter.z) - limits,
                GridCellSize = cellSize,
                GridDimensions = dimensions,
                GridCellCount = dimensions.x * dimensions.y * dimensions.z,
                GoalChangeChance = _flockSettings.GoalChangeChance,
                RandomSeed = _flockSettings.RandomSeed,
                AgentCapacity = _flockSettings.MaximumAgents,
                MaximumNeighbourCandidates = _flockSettings.MaximumNeighbourCandidates,
                MaxDeltaTime = _flockSettings.MaxDeltaTime,
                CohesionWeight = _flockSettings.CohesionWeight,
                AlignmentWeight = _flockSettings.AlignmentWeight,
                GoalWeight = _flockSettings.GoalWeight,
                SeparationWeight = _flockSettings.SeparationWeight,
                MaxAcceleration = _flockSettings.MaxAcceleration,
                BoundaryWeight = _flockSettings.BoundaryWeight,
                BoundaryMargin = _flockSettings.BoundaryMargin,
                OutsideBoundsRotationMultiplier = _flockSettings.OutsideBoundsRotationMultiplier,
            });
        }

        private bool TryGetVisualDefinition(
            GameObject sourcePrefab,
            int visualIndex,
            out EnemyFlockVisual visual)
        {
            if (_visuals.TryGetValue(visualIndex, out visual))
            {
                return true;
            }

            var graphics = _world.GetExistingSystemManaged<EntitiesGraphicsSystem>();
            if (graphics == null)
            {
                if (!_warnedMissingGraphics)
                {
                    Debug.LogError("[EnemyFlock] Entities Graphics System 不存在，无法启动 ECS Flocking。");
                    _warnedMissingGraphics = true;
                }
                return false;
            }

            MeshFilter meshFilter = sourcePrefab.GetComponentInChildren<MeshFilter>(true);
            MeshRenderer renderer = meshFilter != null
                ? meshFilter.GetComponent<MeshRenderer>()
                : sourcePrefab.GetComponentInChildren<MeshRenderer>(true);
            if (meshFilter == null || renderer == null || meshFilter.sharedMesh == null || renderer.sharedMaterial == null)
            {
                Debug.LogError($"[EnemyFlock] {sourcePrefab.name} 缺少 MeshFilter/MeshRenderer，无法创建 ECS Enemy。");
                return false;
            }

            Material material = CreateFlockRenderMaterial(renderer.sharedMaterial, visualIndex);
            if (material == null)
            {
                return false;
            }

            BatchMeshID meshId = graphics.RegisterMesh(meshFilter.sharedMesh);
            BatchMaterialID materialId = graphics.RegisterMaterial(material);
            if (meshId.Equals(BatchMeshID.Null) || materialId.Equals(BatchMaterialID.Null))
            {
                if (!materialId.Equals(BatchMaterialID.Null))
                {
                    graphics.UnregisterMaterial(materialId);
                }

                if (!meshId.Equals(BatchMeshID.Null))
                {
                    graphics.UnregisterMesh(meshId);
                }
                UnityEngine.Object.Destroy(material);
                return false;
            }

            Transform model = meshFilter.transform;
            visual = new EnemyFlockVisual
            {
                MeshId = meshId,
                MaterialId = materialId,
                BaseColor = ToLinearColor(material),
                PostTransform = new PostTransformMatrix
                {
                    Value = float4x4.TRS(
                        new float3(model.localPosition.x, model.localPosition.y, model.localPosition.z),
                        new quaternion(model.localRotation.x, model.localRotation.y, model.localRotation.z, model.localRotation.w),
                        new float3(model.localScale.x, model.localScale.y, model.localScale.z)),
                },
            };
            _visuals.Add(visualIndex, visual);
            _runtimeMaterials.Add(material);
            return true;
        }

        private Entity CreateAgentPrototype(EnemyFlockVisual visual)
        {
            Entity entity = _entityManager.CreateEntity();
            _entityManager.AddComponentData(entity, new LocalTransform
            {
                Position = float3.zero,
                Rotation = quaternion.identity,
                Scale = 1f,
            });
            _entityManager.AddComponentData(entity, new EnemyFlockAgent());
            _entityManager.AddComponentData(entity, new EnemyFlockNextPose
            {
                Position = float3.zero,
                Rotation = quaternion.identity,
            });
            _entityManager.AddComponentData(entity, new EnemyFlockBridgeIndex { Value = -1 });
            _entityManager.AddComponentData(entity, new EnemyFlockActive());
            _entityManager.AddChunkComponentData<EnemyFlockChunkGoal>(entity);
            _entityManager.SetChunkComponentData(_entityManager.GetChunk(entity), new EnemyFlockChunkGoal
            {
                GoalPosition = new float3(
                    _flockSettings.SwimCenter.x,
                    _flockSettings.SwimCenter.y,
                    _flockSettings.SwimCenter.z),
                RandomState = 0u,
                SeedEntityIndex = -1,
            });

            var description = new RenderMeshDescription(
                ShadowCastingMode.Off,
                receiveShadows: false,
                motionVectorGenerationMode: MotionVectorGenerationMode.Camera,
                layer: 6,
                renderingLayerMask: 0xffffffff,
                lightProbeUsage: LightProbeUsage.Off);
            RenderMeshUtility.AddComponents(
                entity,
                _entityManager,
                description,
                new MaterialMeshInfo(visual.MaterialId, visual.MeshId));
            _entityManager.AddComponentData(
                entity,
                new URPMaterialPropertyBaseColor { Value = visual.BaseColor });
            _entityManager.AddComponentData(entity, visual.PostTransform);
            _entityManager.SetComponentEnabled<EnemyFlockActive>(entity, false);
            _entityManager.SetComponentEnabled<MaterialMeshInfo>(entity, false);
            return entity;
        }

        private Material CreateFlockRenderMaterial(Material source, int visualIndex)
        {
            Shader shader = Shader.Find("Custom/EnemyFlockToon") ?? Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                return null;
            }

            var material = new Material(shader)
            {
                name = $"EnemyFlockMaterial_{visualIndex}",
                enableInstancing = true,
            };

            CopyColor(source, material, "_BaseColor");
            CopyColor(source, material, "_ShadowColor");
            CopyColor(source, material, "_SpecularColor");
            CopyColor(source, material, "_RimColor");
            CopyFloat(source, material, "_Glossiness");
            CopyFloat(source, material, "_RimPower");
            CopyFloat(source, material, "_RimThreshold");
            CopyFloat(source, material, "_ShadowStep");
            return material;
        }

        private void SetEntityFlockData(
            EnemyFlockBridge bridge,
            EnemyFlockProfile profile,
            EnemyFlockVisual visual,
            Vector3 position,
            Quaternion rotation)
        {
            float3 entityPosition = new float3(position.x, position.y, position.z);
            quaternion entityRotation = new quaternion(rotation.x, rotation.y, rotation.z, rotation.w);
            float seedRange = Mathf.Max(0.01f, profile.MaxSpeed - profile.MinSpeed);
            float speed = profile.MinSpeed + Mathf.Abs((bridge.Slot * 17.13f) % seedRange);
            speed = Mathf.Clamp(speed, profile.MinSpeed, profile.MaxSpeed);

            _entityManager.SetComponentData(bridge.Entity, new LocalTransform
            {
                Position = entityPosition,
                Rotation = entityRotation,
                Scale = 1f,
            });
            _entityManager.SetComponentData(bridge.Entity, new EnemyFlockNextPose
            {
                Position = entityPosition,
                Rotation = entityRotation,
            });
            EnemyFlockAgent agent = profile.CreateAgentData(speed);
            agent.Velocity = math.mul(
                entityRotation,
                new float3(0f, 0f, speed));
            _entityManager.SetComponentData(bridge.Entity, agent);
            _entityManager.SetComponentData(bridge.Entity, new EnemyFlockBridgeIndex { Value = bridge.Slot });
            _entityManager.SetComponentData(bridge.Entity, visual.PostTransform);
            _entityManager.SetComponentData(bridge.Entity, new MaterialMeshInfo(visual.MaterialId, visual.MeshId));
            _entityManager.SetComponentData(
                bridge.Entity,
                new URPMaterialPropertyBaseColor { Value = visual.BaseColor });
            _entityManager.SetComponentEnabled<EnemyFlockActive>(bridge.Entity, true);
            _entityManager.SetComponentEnabled<MaterialMeshInfo>(bridge.Entity, true);
        }

        private int AllocateSlot()
        {
            if (_freeSlots.Count > 0)
            {
                return _freeSlots.Dequeue();
            }

            return _nextSlot < _flockSettings.MaximumAgents ? _nextSlot++ : -1;
        }

        private EnemyFlockBridge GetAnyBridge()
        {
            foreach (var bridge in _bridgesBySlot.Values)
            {
                return bridge;
            }

            return null;
        }

        private void CompleteJobsBeforeStructuralChange()
        {
            if (_world != null && _world.IsCreated)
            {
                _entityManager.CompleteAllTrackedJobs();
            }
        }

        private static float4 ToLinearColor(Material material)
        {
            Color color = material.HasProperty("_BaseColor")
                ? material.GetColor("_BaseColor")
                : Color.white;
            Color linearColor = color.linear;
            return new float4(
                linearColor.r,
                linearColor.g,
                linearColor.b,
                linearColor.a);
        }

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

        private bool CanUpdatePresentation(EnemyFlockBridge bridge)
        {
            return CanUpdateAgent(bridge)
                && _entityManager.HasComponent<URPMaterialPropertyBaseColor>(bridge.Entity);
        }

        private static void CopyColor(Material source, Material target, string property)
        {
            if (source.HasProperty(property) && target.HasProperty(property))
            {
                target.SetColor(property, source.GetColor(property));
            }
        }

        private static void CopyFloat(Material source, Material target, string property)
        {
            if (source.HasProperty(property) && target.HasProperty(property))
            {
                target.SetFloat(property, source.GetFloat(property));
            }
        }
    }
}
