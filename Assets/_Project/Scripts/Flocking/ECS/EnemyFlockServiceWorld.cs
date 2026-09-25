using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace FlockingSystem.ECS
{
    /// <summary>负责解析 ECS World，并把 ScriptableObject 配置转换成世界组件。</summary>
    public sealed partial class EnemyFlockRuntimeService
    {
        /// <summary>解析默认 ECS World，并取得负责姿态提交的系统。</summary>
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
            _configEntity = Entity.Null;
            _maximumNeighbourDistance = 0f;
            _maximumSeparationDistance = 0f;
            _worldConfigInitialized = false;
            _commitSystem = _world.GetOrCreateSystemManaged<EnemyFlockCommitSystem>();
            _commitSystem.ConfigureAgentCapacity(_flockSettings.MaximumAgents);
            return true;
        }

        /// <summary>确保当前 World 中存在唯一的群游世界配置实体。</summary>
        private void EnsureWorldConfigEntity()
        {
            if (_configEntity != Entity.Null && _entityManager.Exists(_configEntity))
            {
                return;
            }

            using var query = _entityManager.CreateEntityQuery(
                ComponentType.ReadWrite<EnemyFlockWorldConfig>());
            _configEntity = query.IsEmptyIgnoreFilter
                ? _entityManager.CreateEntity(typeof(EnemyFlockWorldConfig))
                : query.GetSingletonEntity();
        }

        /// <summary>把静态配置和已知最大交互距离写入 Burst 使用的世界组件。</summary>
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
    }
}
