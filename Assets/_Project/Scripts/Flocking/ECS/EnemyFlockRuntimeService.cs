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
        private readonly EnemyFlockSettingsSO _settings;
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
            _settings = settings;
        }

        private struct EnemyFlockVisual
        {
            public BatchMaterialID MaterialId;
            public BatchMeshID MeshId;
            public PostTransformMatrix PostTransform;
        }

        /// <summary>
        /// 获取或复用一个 ECS Flocking 实体，并同步其初始姿态和行为配置。
        /// </summary>
        /// <returns>绑定成功返回 true；配置、World、渲染资源或容量不足时返回 false。</returns>
        public bool TryAcquire(
            EnemyFlockBridge bridge,
            GameObject sourcePrefab,
            EnemyType enemyType,
            float speedMultiplier,
            Vector3 position,
            Quaternion rotation)
        {
            if (_disposed || _settings == null || bridge == null || sourcePrefab == null || !TryInitialize())
                return false;

            if (bridge.Runtime != null && bridge.Runtime != this)
                return false;

            EnemyFlockProfile profile = _settings.GetProfile(enemyType, speedMultiplier);

            _maximumNeighbourDistance = Mathf.Max(_maximumNeighbourDistance, profile.NeighbourDistance);
            _maximumSeparationDistance = Mathf.Max(_maximumSeparationDistance, profile.SeparationDistance);
            EnsureConfigEntity();
            WriteWorldConfig();

            if (!TryGetVisual(sourcePrefab, profile.VisualIndex, out EnemyFlockVisual visual))
                return false;

            CompleteJobsBeforeStructuralChange();
            if (_prototype == Entity.Null || !_entityManager.Exists(_prototype))
                _prototype = CreatePrototype(visual);

            bool reuse = bridge.IsEcsControlled && _entityManager.Exists(bridge.Entity);
            if (!reuse && bridge.IsEcsControlled)
                Unbind(bridge);

            if (!reuse)
            {
                int slot = AllocateSlot();
                if (slot < 0)
                    return false;

                Entity entity = _entityManager.Instantiate(_prototype);
                int transformIndex = _commitSystem.RegisterTransform(bridge.transform, slot);
                if (transformIndex < 0)
                {
                    _entityManager.DestroyEntity(entity);
                    _freeSlots.Enqueue(slot);
                    return false;
                }

                bridge.Attach(this, entity, slot, transformIndex, profile.SpeedMultiplier);
                _bridgesBySlot[slot] = bridge;
            }

            EntityFlockSetData(bridge, profile, visual, position, rotation);
            bridge.SetSpeedMultiplierCache(profile.SpeedMultiplier);
            bridge.SetEcsPresentation(true);
            _commitSystem.SetTransformActive(bridge.TransformArrayIndex, true);
            return true;
        }

        /// <summary>
        /// 暂停一个已绑定实体，保留实体和 slot 供对象池复用。
        /// </summary>
        public bool Release(EnemyFlockBridge bridge)
        {
            if (_disposed
                || _world == null
                || !_world.IsCreated
                || bridge == null
                || bridge.Runtime != this
                || !_entityManager.Exists(bridge.Entity))
                return false;

            CompleteJobsBeforeStructuralChange();
            _entityManager.SetComponentEnabled<EnemyFlockActive>(bridge.Entity, false);
            _entityManager.SetComponentEnabled<MaterialMeshInfo>(bridge.Entity, false);
            _commitSystem.SetTransformActive(bridge.TransformArrayIndex, false);
            return true;
        }

        internal void SetSpeedMultiplier(EnemyFlockBridge bridge, float multiplier)
        {
            if (_disposed
                || _world == null
                || !_world.IsCreated
                || bridge == null
                || bridge.Runtime != this
                || !_entityManager.Exists(bridge.Entity))
                return;

            EnemyFlockAgent agent = _entityManager.GetComponentData<EnemyFlockAgent>(bridge.Entity);
            agent.SpeedMultiplier = Mathf.Max(0f, multiplier);
            _entityManager.SetComponentData(bridge.Entity, agent);
        }

        internal void Unbind(EnemyFlockBridge bridge)
        {
            if (bridge == null || bridge.Runtime != this)
                return;

            if (_disposed)
            {
                bridge.ClearBinding();
                return;
            }

            if (_world == null || !_world.IsCreated)
            {
                _bridgesBySlot.Remove(bridge.Slot);
                bridge.ClearBinding();
                return;
            }

            CompleteJobsBeforeStructuralChange();
            int slot = bridge.Slot;
            if (_entityManager.Exists(bridge.Entity))
                _entityManager.DestroyEntity(bridge.Entity);

            int movedSlot = _commitSystem.UnregisterTransform(bridge.TransformArrayIndex);
            if (movedSlot >= 0 && _bridgesBySlot.TryGetValue(movedSlot, out EnemyFlockBridge movedBridge))
                movedBridge.TransformArrayIndex = bridge.TransformArrayIndex;

            _bridgesBySlot.Remove(slot);
            _freeSlots.Enqueue(slot);
            bridge.ClearBinding();
        }

        /// <summary>
        /// 完成 Job、销毁 ECS 实体和原型，并释放运行时渲染资源。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            if (_world != null && _world.IsCreated)
            {
                CompleteJobsBeforeStructuralChange();
                while (_bridgesBySlot.Count > 0)
                    Unbind(GetFirstBridge());

                if (_prototype != Entity.Null && _entityManager.Exists(_prototype))
                    _entityManager.DestroyEntity(_prototype);
                if (_configEntity != Entity.Null && _entityManager.Exists(_configEntity))
                    _entityManager.DestroyEntity(_configEntity);
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
                UnityEngine.Object.Destroy(material);

            _visuals.Clear();
            _runtimeMaterials.Clear();
            _disposed = true;
        }

        private bool TryInitialize()
        {
            if (_settings == null)
            {
                Debug.LogError("[EnemyFlock] EnemyFlockSettings 未配置，无法启动 ECS Flocking。");
                return false;
            }

            if (_world != null && _world.IsCreated)
                return true;

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
            _commitSystem.ConfigureCapacity(_settings.MaximumAgents);
            return true;
        }

        private void EnsureConfigEntity()
        {
            if (_configEntity != Entity.Null && _entityManager.Exists(_configEntity))
                return;

            using var query = _entityManager.CreateEntityQuery(ComponentType.ReadWrite<EnemyFlockWorldConfig>());
            _configEntity = query.IsEmptyIgnoreFilter
                ? _entityManager.CreateEntity(typeof(EnemyFlockWorldConfig))
                : query.GetSingletonEntity();
        }

        private void WriteWorldConfig()
        {
            Vector3 configuredLimits = _settings.SwimLimits;
            float3 limits = new float3(configuredLimits.x, configuredLimits.y, configuredLimits.z);
            float3 extent = limits * 2f;
            float largestExtent = Mathf.Max(extent.x, Mathf.Max(extent.y, extent.z));
            float cellSize = _settings.GridCellSize > 0f
                ? _settings.GridCellSize
                : Mathf.Max(
                    Mathf.Max(_maximumNeighbourDistance, _maximumSeparationDistance),
                    largestExtent / _settings.GridCellsPerAxis);
            int maximumDimension = _settings.GridCellsPerAxis;
            int3 dimensions = new int3(
                Mathf.Clamp(Mathf.CeilToInt(extent.x / cellSize), 1, maximumDimension),
                Mathf.Clamp(Mathf.CeilToInt(extent.y / cellSize), 1, maximumDimension),
                Mathf.Clamp(Mathf.CeilToInt(extent.z / cellSize), 1, maximumDimension));
            Vector3 configuredCenter = _settings.SwimCenter;

            _entityManager.SetComponentData(_configEntity, new EnemyFlockWorldConfig
            {
                SwimCenter = new float3(configuredCenter.x, configuredCenter.y, configuredCenter.z),
                SwimLimits = limits,
                GridOrigin = new float3(configuredCenter.x, configuredCenter.y, configuredCenter.z) - limits,
                GridCellSize = cellSize,
                GridDimensions = dimensions,
                GridCellCount = dimensions.x * dimensions.y * dimensions.z,
                GoalChangeChance = _settings.GoalChangeChance,
                RandomSeed = _settings.RandomSeed,
                AgentCapacity = _settings.MaximumAgents,
                MaximumNeighbourCandidates = _settings.MaximumNeighbourCandidates,
                MaxDeltaTime = _settings.MaxDeltaTime,
                CohesionWeight = _settings.CohesionWeight,
                AlignmentWeight = _settings.AlignmentWeight,
                GoalWeight = _settings.GoalWeight,
                SeparationWeight = _settings.SeparationWeight,
                MaxAcceleration = _settings.MaxAcceleration,
                BoundaryWeight = _settings.BoundaryWeight,
                BoundaryMargin = _settings.BoundaryMargin,
                OutsideBoundsRotationMultiplier = _settings.OutsideBoundsRotationMultiplier,
            });
        }

        private bool TryGetVisual(GameObject sourcePrefab, int visualIndex, out EnemyFlockVisual visual)
        {
            if (_visuals.TryGetValue(visualIndex, out visual))
                return true;

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

            Material material = CreateRenderMaterial(renderer.sharedMaterial, visualIndex);
            if (material == null)
                return false;

            BatchMeshID meshId = graphics.RegisterMesh(meshFilter.sharedMesh);
            BatchMaterialID materialId = graphics.RegisterMaterial(material);
            if (meshId.Equals(BatchMeshID.Null) || materialId.Equals(BatchMaterialID.Null))
            {
                if (!materialId.Equals(BatchMaterialID.Null))
                    graphics.UnregisterMaterial(materialId);
                if (!meshId.Equals(BatchMeshID.Null))
                    graphics.UnregisterMesh(meshId);
                UnityEngine.Object.Destroy(material);
                return false;
            }

            Transform model = meshFilter.transform;
            visual = new EnemyFlockVisual
            {
                MeshId = meshId,
                MaterialId = materialId,
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

        private Entity CreatePrototype(EnemyFlockVisual visual)
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
                GoalPosition = new float3(_settings.SwimCenter.x, _settings.SwimCenter.y, _settings.SwimCenter.z),
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
            _entityManager.AddComponentData(entity, visual.PostTransform);
            _entityManager.SetComponentEnabled<EnemyFlockActive>(entity, false);
            _entityManager.SetComponentEnabled<MaterialMeshInfo>(entity, false);
            return entity;
        }

        private Material CreateRenderMaterial(Material source, int visualIndex)
        {
            Shader shader = Shader.Find("Custom/EnemyFlockToon") ?? Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                return null;

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

        private void EntityFlockSetData(
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
            EnemyFlockAgent agent = profile.ToAgent(speed);
            agent.Velocity = math.mul(
                entityRotation,
                new float3(0f, 0f, speed));
            _entityManager.SetComponentData(bridge.Entity, agent);
            _entityManager.SetComponentData(bridge.Entity, new EnemyFlockBridgeIndex { Value = bridge.Slot });
            _entityManager.SetComponentData(bridge.Entity, visual.PostTransform);
            _entityManager.SetComponentData(bridge.Entity, new MaterialMeshInfo(visual.MaterialId, visual.MeshId));
            _entityManager.SetComponentEnabled<EnemyFlockActive>(bridge.Entity, true);
            _entityManager.SetComponentEnabled<MaterialMeshInfo>(bridge.Entity, true);
        }

        private int AllocateSlot()
        {
            if (_freeSlots.Count > 0)
                return _freeSlots.Dequeue();
            return _nextSlot < _settings.MaximumAgents ? _nextSlot++ : -1;
        }

        private EnemyFlockBridge GetFirstBridge()
        {
            foreach (var bridge in _bridgesBySlot.Values)
                return bridge;
            return null;
        }

        private void CompleteJobsBeforeStructuralChange()
        {
            if (_world != null && _world.IsCreated)
                _entityManager.CompleteAllTrackedJobs();
        }

        private static void CopyColor(Material source, Material target, string property)
        {
            if (source.HasProperty(property) && target.HasProperty(property))
                target.SetColor(property, source.GetColor(property));
        }

        private static void CopyFloat(Material source, Material target, string property)
        {
            if (source.HasProperty(property) && target.HasProperty(property))
                target.SetFloat(property, source.GetFloat(property));
        }
    }
}
