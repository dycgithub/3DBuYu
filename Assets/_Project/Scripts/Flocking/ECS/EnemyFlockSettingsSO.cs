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
        private const int MaximumGridCellsPerAxis = 8;
        private const float MinimumAgentDistance = 0.01f;

        [Header("World")]
        [SerializeField] private Vector3 swimCenter = Vector3.zero;
        [SerializeField] private Vector3 swimLimits = new(50f, 40f, 50f);

        [Header("Goal")]
        [SerializeField, Range(0f, 1f)] private float goalChangeChance = 0.005f;
        [SerializeField] private uint randomSeed = 0x6D2B79F5u;

        [Header("Grid")]
        [Tooltip("设为 0 时按区域和轴数自动计算。")]
        [SerializeField, Min(0f)] private float gridCellSize;
        [SerializeField, Range(1, MaximumGridCellsPerAxis)] private int gridCellsPerAxis = MaximumGridCellsPerAxis;

        [Header("Simulation")]
        [SerializeField, Min(0.001f)] private float maxDeltaTime = 0.1f;
        [Tooltip("邻居平均速度向量的对齐权重。")]
        [SerializeField, Min(0f)] private float alignmentWeight = 1f;
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
        [SerializeField, Range(1, EnemyFlockLimits.MaximumNeighbourCandidates)]
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

        [SerializeField] private ProfileSettings slow = new()
        {
            VisualIndex = 2,
            NeighbourDistance = 5f,
            SeparationDistance = 2.5f,
            RotationSpeed = 3f,
            MinSpeed = 1f,
            MaxSpeed = 2f,
        };

        /// <summary>游动区域中心的世界坐标。</summary>
        public Vector3 SwimCenter => swimCenter;

        /// <summary>游动区域相对中心的半尺寸，单位为世界单位。</summary>
        public Vector3 SwimLimits => new(
            Mathf.Max(0.1f, Mathf.Abs(swimLimits.x)),
            Mathf.Max(0.1f, Mathf.Abs(swimLimits.y)),
            Mathf.Max(0.1f, Mathf.Abs(swimLimits.z)));

        /// <summary>每个模拟步改变 Chunk 目标点的概率。</summary>
        public float GoalChangeChance => Mathf.Clamp01(goalChangeChance);

        /// <summary>生成 Chunk 目标随机状态的非零世界种子。</summary>
        public uint RandomSeed => randomSeed == 0 ? 1u : randomSeed;

        /// <summary>空间网格单元边长；零表示由区域尺寸自动计算。</summary>
        public float GridCellSize => Mathf.Max(0f, gridCellSize);

        /// <summary>每个轴的最大网格单元数量。</summary>
        public int GridCellsPerAxis => Mathf.Clamp(
            gridCellsPerAxis,
            1,
            MaximumGridCellsPerAxis);

        /// <summary>单次模拟允许使用的最大步长，单位为秒。</summary>
        public float MaxDeltaTime => Mathf.Max(0.001f, maxDeltaTime);

        /// <summary>邻居平均速度的对齐权重。</summary>
        public float AlignmentWeight => Mathf.Max(0f, alignmentWeight);

        /// <summary>邻居平均位置的聚拢权重。</summary>
        public float CohesionWeight => Mathf.Max(0f, cohesionWeight);

        /// <summary>Chunk 目标点的移动权重。</summary>
        public float GoalWeight => Mathf.Max(0f, goalWeight);

        /// <summary>邻居分离行为的权重。</summary>
        public float SeparationWeight => Mathf.Max(0f, separationWeight);

        /// <summary>单个模拟步允许施加的最大加速度。</summary>
        public float MaxAcceleration => Mathf.Max(0f, maxAcceleration);

        /// <summary>边界回推行为的权重。</summary>
        public float BoundaryWeight => Mathf.Max(0f, boundaryWeight);

        /// <summary>距离边界多远时开始施加边界力。</summary>
        public float BoundaryMargin => Mathf.Max(0.001f, boundaryMargin);

        /// <summary>代理越界时的旋转速度倍率。</summary>
        public float OutsideBoundsRotationMultiplier => Mathf.Max(0f, outsideBoundsRotationMultiplier);

        /// <summary>群游世界允许使用的最大代理数量。</summary>
        public int MaximumAgents => Mathf.Clamp(maximumAgents, 1, EnemyFlockLimits.MaximumAgents);

        /// <summary>一次邻居查询允许检查的最大候选数量。</summary>
        public int MaximumNeighbourCandidates => Mathf.Clamp(
            maximumNeighbourCandidates, 1, EnemyFlockLimits.MaximumNeighbourCandidates);

        /// <summary>
        /// 根据敌人类型读取配置，并将当前速度倍率写入运行时群游配置。
        /// </summary>
        /// <param name="enemyType">要读取的敌人类型。</param>
        /// <param name="speedMultiplier">敌人当前的速度倍率。</param>
        /// <returns>可供 ECS 实体使用的群游配置。</returns>
        public EnemyFlockProfile GetProfileForEnemyType(EnemyType enemyType, float speedMultiplier)
        {
            ProfileSettings profile = enemyType switch
            {
                EnemyType.Fast => fast,
                EnemyType.Tank => slow,
                _ => normal,
            };

            return profile.CreateRuntimeProfile(speedMultiplier);
        }

        [Serializable]
        private sealed class ProfileSettings
        {
            /// <summary>对应的 ECS 网格和材质视觉编号。</summary>
            [SerializeField] public int VisualIndex;

            /// <summary>聚拢和对齐的邻居距离，单位为世界单位。</summary>
            [SerializeField, Min(MinimumAgentDistance)] public float NeighbourDistance = 5f;

            /// <summary>分离力的作用距离，单位为世界单位。</summary>
            [SerializeField, Min(MinimumAgentDistance)] public float SeparationDistance = 2f;

            /// <summary>代理每秒最大旋转角度，单位为度。</summary>
            [SerializeField, Min(0f)] public float RotationSpeed = 4f;

            /// <summary>代理最小速度，单位为世界单位/秒。</summary>
            [SerializeField, Min(0f)] public float MinSpeed = 1f;

            /// <summary>代理最大速度，单位为世界单位/秒。</summary>
            [SerializeField, Min(0f)] public float MaxSpeed = 3f;

            /// <summary>
            /// 将 Inspector 配置转换为不可变的运行时群游配置。
            /// </summary>
            /// <param name="speedMultiplier">运行时速度倍率。</param>
            /// <returns>供 EnemyFlockProfile 继续归一化的运行时配置。</returns>
            public EnemyFlockProfile CreateRuntimeProfile(float speedMultiplier)
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
