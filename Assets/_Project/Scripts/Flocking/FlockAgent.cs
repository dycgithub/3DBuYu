using UnityEngine;
#if UNITY_ENTITIES
using Unity.Entities;
#endif
using VContainer;

namespace FlockingSystem
{
    /// <summary>
    /// 通用群游代理 — 为装饰鱼群和敌人提供 Boids 驱动的移动。
    ///
    /// 每帧自动执行聚合/分离/对齐规则 + 边界约束。
    /// 外部系统可通过 <see cref="SpeedMultiplier"/> 控制速度（如状态效果）。
    ///
    /// 使用方式：
    /// 1. 挂载到任何需要群游行为的 GameObject 上
    /// 2. 场景中需要有一个 FlockManager 实例
    /// 3. 通过在 Start 中调用 FlockManager.Register(this) 加入群组
    /// </summary>
    public class FlockAgent : MonoBehaviour
    {
        [Header("Boids 参数")]
        [Tooltip("邻居检测半径（聚合+对齐范围）。")]
        [SerializeField] private float neighbourDistance = 5f;

        [Tooltip("分离触发距离（避免碰撞）。")]
        [SerializeField] private float separationDistance = 2f;

        [Tooltip("旋转平滑速度。")]
        [SerializeField] private float rotationSpeed = 4f;

        [Tooltip("最小游动速度。")]
        [SerializeField] private float minSpeed = 2f;

        [Tooltip("最大游动速度。")]
        [SerializeField] private float maxSpeed = 5f;

        [Header("性能")]
        [Tooltip("ECS 接管后此值不再生效。")]
        [SerializeField, Range(0f, 1f)] private float ruleApplyChance = 0.2f;

        #region Public Accessors

        /// <summary>邻居检测半径。</summary>
        public float NeighbourDistance
        {
            get => neighbourDistance;
            set => neighbourDistance = value;
        }

        /// <summary>分离触发距离。</summary>
        public float SeparationDistance
        {
            get => separationDistance;
            set => separationDistance = value;
        }

        /// <summary>旋转平滑速度。</summary>
        public float RotationSpeed
        {
            get => rotationSpeed;
            set => rotationSpeed = value;
        }

        /// <summary>最小游动速度。</summary>
        public float MinSpeed
        {
            get => minSpeed;
            set => minSpeed = value;
        }

        /// <summary>最大游动速度。</summary>
        public float MaxSpeed
        {
            get => maxSpeed;
            set => maxSpeed = value;
        }

        /// <summary>当前速度。</summary>
        public float Speed { get; private set; }

        /// <summary>
        /// 外部速度倍率（如冰冻效果减速）。
        /// 由外部系统（EnemyBase/状态效果）在 Update 中设置。
        /// </summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>关联的群游管理器。</summary>
        public FlockManager Manager { get; private set; }

        #endregion

        #region Unity Lifecycle

        private Transform cachedTransform;

        void Awake()
        {
            cachedTransform = transform;
            Speed = Random.Range(minSpeed, maxSpeed);
        }

        [Inject] private FlockManager _injectedManager;

        void Start()
        {
            if (Manager == null && _injectedManager != null)
            {
                Manager = _injectedManager;
                Manager.Register(this);
            }
        }

        void Update()
        {
            // ECS 接管模式：不执行运行时逻辑
            if (IsECSActive()) return;

            ApplyMovementInternal();
        }

        void OnDestroy()
        {
            if (Manager != null)
                Manager.Unregister(this);
        }

        #endregion

        #region Public API

        /// <summary>
        /// 初始化并加入指定群组。
        /// </summary>
        public void Initialize(FlockManager manager)
        {
            Manager = manager;
            Speed = Random.Range(minSpeed, maxSpeed);
            manager.Register(this);
        }

        /// <summary>
        /// 外部调用：执行一帧群游移动。
        /// EnemyBase 等系统可用此方法替代 Update 自主移动。
        /// </summary>
        public void ApplyMovement(float deltaTime)
        {
            if (IsECSActive()) return;
            ApplyMovementInternal(deltaTime);
        }

        #endregion

        #region Movement Logic

        private void ApplyMovementInternal(float? overrideDeltaTime = null)
        {
            float dt = overrideDeltaTime ?? Time.deltaTime;

            if (Manager == null) return;

            bool outsideBounds = !Manager.SwimBounds.Contains(cachedTransform.position);

            if (outsideBounds)
            {
                SteerToward(Manager.SwimCenter);
            }
            else if (Random.value < ruleApplyChance)
            {
                ApplyBoidsRules();
            }

            // 钳制速度并向前移动
            Speed = Mathf.Clamp(Speed, minSpeed, maxSpeed);
            float effectiveSpeed = Speed * SpeedMultiplier;
            cachedTransform.Translate(Vector3.forward * (effectiveSpeed * dt));
        }

        /// <summary>
        /// 强制转向目标点（边界折返时使用）。
        /// </summary>
        private void SteerToward(Vector3 target)
        {
            Vector3 direction = target - cachedTransform.position;
            cachedTransform.rotation = Quaternion.Slerp(
                cachedTransform.rotation,
                Quaternion.LookRotation(direction),
                rotationSpeed * 2f * Time.deltaTime);
        }

        /// <summary>
        /// Boids 三规则：聚合 + 分离 + 对齐。
        /// O(n²) 遍历所有同群代理。
        /// </summary>
        private void ApplyBoidsRules()
        {
            if (Manager == null || Manager.Agents == null) return;

            var agents = Manager.Agents;
            Vector3 cohesion = Vector3.zero;
            Vector3 avoidance = Vector3.zero;
            float totalSpeed = 0f;
            int groupSize = 0;
            Vector3 myPos = cachedTransform.position;

            foreach (var other in agents)
            {
                if (other == this || other == null) continue;

                Vector3 otherPos = other.transform.position;
                float dist = Vector3.Distance(otherPos, myPos);
                if (dist > neighbourDistance) continue;

                // 聚合
                cohesion += otherPos;
                groupSize++;
                totalSpeed += other.Speed;

                // 分离
                if (dist < separationDistance && dist > 0.001f)
                {
                    avoidance += (myPos - otherPos).normalized;
                }
            }

            if (groupSize == 0) return;

            // 聚合方向
            Vector3 avgCenter = cohesion / groupSize;
            Vector3 cohesionDir = (avgCenter + Manager.GoalPos) * 0.5f - myPos;

            // 对齐：平滑匹配邻居平均速度
            float avgSpeed = totalSpeed / groupSize;
            Speed = Mathf.Lerp(Speed, avgSpeed, Time.deltaTime * 3f);

            // 最终方向 = 聚合 + 分离(加权)
            Vector3 finalDirection = cohesionDir + avoidance * separationDistance;

            if (finalDirection != Vector3.zero)
            {
                cachedTransform.rotation = Quaternion.Slerp(
                    cachedTransform.rotation,
                    Quaternion.LookRotation(finalDirection),
                    rotationSpeed * Time.deltaTime);
            }
        }

        #endregion

        #region ECS Detection

        private static bool IsECSActive()
        {
#if UNITY_ENTITIES
            foreach (var world in World.All)
            {
                if (world.IsCreated && world.GetExistingSystem<ECS.FlockBoidsSystem>() != default)
                    return true;
            }
#endif
            return false;
        }

        #endregion
    }
}
