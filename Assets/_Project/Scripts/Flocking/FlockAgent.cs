using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace FlockingSystem
{
    /// <summary>
    /// 通用群游代理 — 为装饰鱼群和敌人提供 Boids 驱动的移动。
    ///
    /// 保留用于非 ECS 场景的 Mono Flocking 行为；Enemy 运行时由 ECS 接管。
    ///
    /// 使用方式：
    /// 1. 挂载到任何需要群游行为的 GameObject 上
    /// 2. 场景中需要有一个 FlockManager 实例
    /// 3. 通过调用 <see cref="Initialize"/> 加入群组
    /// </summary>
    public class FlockAgent : MonoBehaviour
    {
        [Header("Boids 参数")]
        [Tooltip("邻居检测半径（聚合+对齐范围）。")]
        [FormerlySerializedAs("neighbourDistance")]
        [SerializeField] private float _neighbourDistance = 5f;

        [Tooltip("分离触发距离（避免碰撞）。")]
        [FormerlySerializedAs("separationDistance")]
        [SerializeField] private float _separationDistance = 2f;

        [Tooltip("旋转平滑速度。")]
        [FormerlySerializedAs("rotationSpeed")]
        [SerializeField] private float _rotationSpeed = 4f;

        [Tooltip("最小游动速度。")]
        [FormerlySerializedAs("minSpeed")]
        [SerializeField] private float _minSpeed = 2f;

        [Tooltip("最大游动速度。")]
        [FormerlySerializedAs("maxSpeed")]
        [SerializeField] private float _maxSpeed = 5f;

        [Header("性能")]
        [Tooltip("ECS 接管后此值不再生效。")]
        [FormerlySerializedAs("ruleApplyChance")]
        [SerializeField, Range(0f, 1f)] private float _boidsRuleApplicationChance = 0.2f;

        #region Public Accessors

        /// <summary>邻居检测半径。</summary>
        public float NeighbourDistance
        {
            get => _neighbourDistance;
            set => _neighbourDistance = value;
        }

        /// <summary>分离触发距离。</summary>
        public float SeparationDistance
        {
            get => _separationDistance;
            set => _separationDistance = value;
        }

        /// <summary>旋转平滑速度。</summary>
        public float RotationSpeed
        {
            get => _rotationSpeed;
            set => _rotationSpeed = value;
        }

        /// <summary>最小游动速度。</summary>
        public float MinSpeed
        {
            get => _minSpeed;
            set => _minSpeed = value;
        }

        /// <summary>最大游动速度。</summary>
        public float MaxSpeed
        {
            get => _maxSpeed;
            set => _maxSpeed = value;
        }

        /// <summary>当前群游速度。</summary>
        public float CurrentSpeed { get; private set; }

        /// <summary>
        /// 外部速度倍率（如冰冻效果减速）。
        /// 由外部系统或状态效果在 Update 中设置。
        /// </summary>
        public float SpeedMultiplier { get; set; } = 1f;

        /// <summary>关联的群游管理器。</summary>
        public FlockManager FlockManager { get; private set; }

        #endregion

        #region Unity Lifecycle

        private Transform _cachedTransform;

        /// <summary>
        /// 缓存 Transform，并为代理生成初始速度。
        /// </summary>
        private void Awake()
        {
            _cachedTransform = transform;
            CurrentSpeed = Random.Range(_minSpeed, _maxSpeed);
        }

        /// <summary>
        /// 使用 Unity 帧时间推进一帧群游移动。
        /// </summary>
        private void Update()
        {
            SimulateMovement();
        }

        /// <summary>
        /// 销毁代理时从所属群组注销。
        /// </summary>
        private void OnDestroy()
        {
            if (FlockManager != null)
            {
                FlockManager.UnregisterAgent(this);
            }
        }

        #endregion

        #region Public API

        /// <summary>
        /// 初始化并加入指定群组。
        /// </summary>
        public void Initialize(FlockManager manager)
        {
            FlockManager = manager;
            CurrentSpeed = Random.Range(_minSpeed, _maxSpeed);
            manager.RegisterAgent(this);
        }

        /// <summary>
        /// 外部调用：执行一帧群游移动。
        /// 外部系统可用此方法替代 Update 自主移动。
        /// </summary>
        public void ApplyMovement(float deltaTime)
        {
            SimulateMovement(deltaTime);
        }

        #endregion

        #region Movement Logic

        /// <summary>
        /// 根据群组边界、Boids 规则和速度倍率推进代理位置。
        /// </summary>
        /// <param name="overrideDeltaTime">可选的模拟时间；为空时使用 Unity 帧时间。</param>
        private void SimulateMovement(float? overrideDeltaTime = null)
        {
            float deltaTime = overrideDeltaTime ?? Time.deltaTime;

            if (FlockManager == null)
            {
                return;
            }

            bool outsideBounds = !FlockManager.SwimBounds.Contains(_cachedTransform.position);

            if (outsideBounds)
            {
                SteerToward(FlockManager.SwimCenter);
            }
            else if (Random.value < _boidsRuleApplicationChance)
            {
                ApplyBoidsSteering();
            }

            // 钳制速度并向前移动
            CurrentSpeed = Mathf.Clamp(CurrentSpeed, _minSpeed, _maxSpeed);
            float effectiveSpeed = CurrentSpeed * SpeedMultiplier;
            _cachedTransform.Translate(Vector3.forward * (effectiveSpeed * deltaTime));
        }

        /// <summary>
        /// 强制转向目标点（边界折返时使用）。
        /// </summary>
        private void SteerToward(Vector3 target)
        {
            Vector3 direction = target - _cachedTransform.position;
            _cachedTransform.rotation = Quaternion.Slerp(
                _cachedTransform.rotation,
                Quaternion.LookRotation(direction),
                _rotationSpeed * 2f * Time.deltaTime);
        }

        /// <summary>
        /// Boids 三规则：聚合 + 分离 + 对齐。
        /// O(n²) 遍历所有同群代理。
        /// </summary>
        private void ApplyBoidsSteering()
        {
            if (FlockManager == null || FlockManager.RegisteredAgents == null)
            {
                return;
            }

            List<FlockAgent> registeredAgents = FlockManager.RegisteredAgents;
            Vector3 cohesion = Vector3.zero;
            Vector3 avoidance = Vector3.zero;
            float totalSpeed = 0f;
            int groupSize = 0;
            Vector3 agentPosition = _cachedTransform.position;

            foreach (FlockAgent otherAgent in registeredAgents)
            {
                if (otherAgent == this || otherAgent == null)
                {
                    continue;
                }

                Vector3 otherAgentPosition = otherAgent.transform.position;
                float distance = Vector3.Distance(otherAgentPosition, agentPosition);
                if (distance > _neighbourDistance)
                {
                    continue;
                }

                // 聚合
                cohesion += otherAgentPosition;
                groupSize++;
                totalSpeed += otherAgent.CurrentSpeed;

                // 分离
                if (distance < _separationDistance && distance > 0.001f)
                {
                    avoidance += (agentPosition - otherAgentPosition).normalized;
                }
            }

            if (groupSize == 0)
            {
                return;
            }

            // 聚合方向
            Vector3 averageNeighbourPosition = cohesion / groupSize;
            Vector3 cohesionDirection = (averageNeighbourPosition + FlockManager.GoalPosition)
                * 0.5f - agentPosition;

            // 对齐：平滑匹配邻居平均速度
            float avgSpeed = totalSpeed / groupSize;
            CurrentSpeed = Mathf.Lerp(CurrentSpeed, avgSpeed, Time.deltaTime * 3f);

            // 最终方向 = 聚合 + 分离(加权)
            Vector3 finalDirection = cohesionDirection + avoidance * _separationDistance;

            if (finalDirection != Vector3.zero)
            {
                _cachedTransform.rotation = Quaternion.Slerp(
                    _cachedTransform.rotation,
                    Quaternion.LookRotation(finalDirection),
                    _rotationSpeed * Time.deltaTime);
            }
        }

        #endregion

    }
}
