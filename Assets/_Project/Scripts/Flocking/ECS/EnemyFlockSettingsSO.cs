using System;
using EnemySystem;
using UnityEngine;
using UnityEngine.Serialization;

namespace FlockingSystem.ECS
{
    /// <summary>
    /// ECS Flocking 的静态配置资产。
    /// 运行时由 GameLoopLifetimeScope 注册，并转换为 Burst Job 使用的世界配置。
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyFlockSettings", menuName = "Flocking/Enemy Flock Settings")]
    public sealed class EnemyFlockSettingsSO : ScriptableObject
    {
        [Header("World")]
        [SerializeField] private Vector3 swimCenter = Vector3.zero;
        [SerializeField] private Vector3 swimLimits = new(50f, 40f, 50f);

        [Header("Goal")]
        [SerializeField, Range(0f, 1f)] private float goalChangeChance = 0.005f;
        [SerializeField] private uint randomSeed = 0x6D2B79F5u;

        [Header("Grid")]
        [Tooltip("设为 0 时按区域和轴数自动计算。")]
        [SerializeField, Min(0f)] private float gridCellSize;
        [SerializeField, Range(1, 8)] private int gridCellsPerAxis = 8;

        [Header("Simulation")]
        [SerializeField, Min(0.001f)] private float maxDeltaTime = 0.1f;
        [Tooltip("邻居平均速度向量的对齐权重。")]
        [SerializeField, Min(0f), FormerlySerializedAs("speedMatching")] private float alignmentWeight = 1f;
        [SerializeField, Min(0f)] private float cohesionWeight = 0.5f;
        [SerializeField, Min(0f)] private float goalWeight = 0.5f;
        [SerializeField, Min(0f)] private float separationWeight = 1f;
        [Tooltip("每秒可施加的最大转向加速度。")]
        [SerializeField, Min(0f)] private float maxAcceleration = 10f;
        [Tooltip("边界内向力的权重。")]
        [SerializeField, Min(0f)] private float boundaryWeight = 2f;
        [Tooltip("距离边界多远时开始施加边界力。")]
        [SerializeField, Min(0.001f)] private float boundaryMargin = 5f;
        [SerializeField, Min(0f)] private float outsideBoundsRotationMultiplier = 2f;

        [Header("Capacity")]
        [SerializeField, Range(1, EnemyFlockLimits.MaximumAgents)] private int maximumAgents = EnemyFlockLimits.MaximumAgents;
        [Tooltip("一次邻居查询最多检查的候选 slot 数量。")]
        [SerializeField, Range(1, EnemyFlockLimits.MaximumNeighbourCandidates), FormerlySerializedAs("maximumSeparationCandidates")]
        private int maximumNeighbourCandidates = EnemyFlockLimits.MaximumNeighbourCandidates;

        [Header("Enemy Profiles")]
        [SerializeField] private ProfileSettings normal = new()
        {
            VisualIndex = 0,
            NeighbourDistance = 5f,
            SeparationDistance = 2f,
            RotationSpeed = 5f,
            MinSpeed = 1f,
            MaxSpeed = 3f,
        };

        [SerializeField] private ProfileSettings fast = new()
        {
            VisualIndex = 1,
            NeighbourDistance = 5f,
            SeparationDistance = 2f,
            RotationSpeed = 6f,
            MinSpeed = 2f,
            MaxSpeed = 5f,
        };

        [SerializeField] private ProfileSettings tank = new()
        {
            VisualIndex = 2,
            NeighbourDistance = 5f,
            SeparationDistance = 2.5f,
            RotationSpeed = 3f,
            MinSpeed = 1f,
            MaxSpeed = 2f,
        };

        public Vector3 SwimCenter => swimCenter;
        public Vector3 SwimLimits => new(
            Mathf.Max(0.1f, Mathf.Abs(swimLimits.x)),
            Mathf.Max(0.1f, Mathf.Abs(swimLimits.y)),
            Mathf.Max(0.1f, Mathf.Abs(swimLimits.z)));
        public float GoalChangeChance => Mathf.Clamp01(goalChangeChance);
        public uint RandomSeed => randomSeed == 0 ? 1u : randomSeed;
        public float GridCellSize => Mathf.Max(0f, gridCellSize);
        public int GridCellsPerAxis => Mathf.Clamp(gridCellsPerAxis, 1, 8);
        public float MaxDeltaTime => Mathf.Max(0.001f, maxDeltaTime);
        public float AlignmentWeight => Mathf.Max(0f, alignmentWeight);
        public float CohesionWeight => Mathf.Max(0f, cohesionWeight);
        public float GoalWeight => Mathf.Max(0f, goalWeight);
        public float SeparationWeight => Mathf.Max(0f, separationWeight);
        public float MaxAcceleration => Mathf.Max(0f, maxAcceleration);
        public float BoundaryWeight => Mathf.Max(0f, boundaryWeight);
        public float BoundaryMargin => Mathf.Max(0.001f, boundaryMargin);
        public float OutsideBoundsRotationMultiplier => Mathf.Max(0f, outsideBoundsRotationMultiplier);
        public int MaximumAgents => Mathf.Clamp(maximumAgents, 1, EnemyFlockLimits.MaximumAgents);
        public int MaximumNeighbourCandidates => Mathf.Clamp(
            maximumNeighbourCandidates, 1, EnemyFlockLimits.MaximumNeighbourCandidates);

        public EnemyFlockProfile GetProfile(EnemyType enemyType, float speedMultiplier)
        {
            ProfileSettings profile = enemyType switch
            {
                EnemyType.Fast => fast,
                EnemyType.Tank => tank,
                _ => normal,
            };

            return profile.ToRuntime(speedMultiplier);
        }

        [Serializable]
        private sealed class ProfileSettings
        {
            [SerializeField] public int VisualIndex;
            [SerializeField, Min(0.01f)] public float NeighbourDistance = 5f;
            [SerializeField, Min(0.01f)] public float SeparationDistance = 2f;
            [SerializeField, Min(0f)] public float RotationSpeed = 4f;
            [SerializeField, Min(0f)] public float MinSpeed = 1f;
            [SerializeField, Min(0f)] public float MaxSpeed = 3f;

            public EnemyFlockProfile ToRuntime(float speedMultiplier)
            {
                return new EnemyFlockProfile(
                    VisualIndex,
                    NeighbourDistance,
                    SeparationDistance,
                    RotationSpeed,
                    MinSpeed,
                    MaxSpeed,
                    speedMultiplier);
            }
        }
    }
}
