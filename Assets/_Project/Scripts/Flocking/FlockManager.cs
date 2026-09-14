using UnityEngine;
using System.Collections.Generic;

namespace FlockingSystem
{
    /// <summary>
    /// 通用群游管理器，维护非 ECS 代理的目标位置和活动边界。
    ///
    /// 管理一组 FlockAgent 的群游行为：
    /// - 随机游走目标点
    /// - 游泳边界约束
    /// - 全局速度控制
    ///
    /// 不负责生成代理，代理由外部系统(敌人生成器/装饰鱼生成器等)
    /// 通过 <see cref="Register"/> 注册。
    /// Enemy 的 ECS 路径由 EnemyFactory 管理；此处保留 Mono FlockAgent 的回退组和区域参数。
    /// </summary>
    public class FlockManager : MonoBehaviour
    {
        [Header("区域")]
        [Tooltip("游泳区域半尺寸。")]
        [SerializeField] private Vector3 swimLimits = new Vector3(5, 5, 5);

        [Header("行为")]
        [Tooltip("目标点变更概率（每帧）。")]
        [SerializeField, Range(0f, 1f)] private float goalChangeChance = 0.005f;

        #region Public Accessors

        /// <summary>群游趋向目标位置（世界坐标）。</summary>
        public Vector3 GoalPosition { get; private set; }

        /// <summary>游泳区域边界（世界坐标 AABB）。</summary>
        public Bounds SwimBounds { get; private set; }

        /// <summary>游泳区域中心（世界坐标）。</summary>
        public Vector3 SwimCenter => transform.position;

        /// <summary>游泳区域半尺寸。</summary>
        public Vector3 SwimLimits => swimLimits;

        /// <summary>所有已注册的群游代理。</summary>
        public List<FlockAgent> RegisteredAgents { get; private set; }

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// 创建非 ECS 群游代理列表。
        /// </summary>
        private void Awake()
        {
            RegisteredAgents = new List<FlockAgent>(50);
        }

        /// <summary>
        /// 将初始目标位置设置为管理器所在位置。
        /// </summary>
        private void Start()
        {
            GoalPosition = transform.position;

        }

        /// <summary>
        /// 更新游泳边界，并按概率生成新的群游目标位置。
        /// </summary>
        private void Update()
        {
            SwimBounds = new Bounds(transform.position, swimLimits * 2f);

            // 随机更新目标点
            if (Random.value < goalChangeChance)
            {
                GoalPosition = transform.position + new Vector3(
                    Random.Range(-swimLimits.x, swimLimits.x),
                    Random.Range(-swimLimits.y, swimLimits.y),
                    Random.Range(-swimLimits.z, swimLimits.z));
            }

        }

        #endregion

        #region Public API

        /// <summary>
        /// 注册代理到此群组。
        /// </summary>
        public void RegisterAgent(FlockAgent agent)
        {
            if (agent == null || RegisteredAgents.Contains(agent))
            {
                return;
            }

            RegisteredAgents.Add(agent);
        }

        /// <summary>
        /// 从群组注销代理。
        /// </summary>
        public void UnregisterAgent(FlockAgent agent)
        {
            RegisteredAgents.Remove(agent);
        }

        /// <summary>
        /// 批量设置所有代理的速度倍率（如冰冻/加速效果）。
        /// </summary>
        public void SetAllAgentSpeedMultiplier(float multiplier)
        {
            foreach (FlockAgent agent in RegisteredAgents)
            {
                if (agent != null)
                {
                    agent.SpeedMultiplier = multiplier;
                }
            }

        }

        /// <summary>
        /// 更改游泳区域配置。
        /// </summary>
        public void SetSwimLimits(Vector3 limits)
        {
            swimLimits = limits;
        }

        /// <summary>
        /// 移除所有已死亡的代理（null 引用清理）。
        /// </summary>
        public void CleanupNullAgents()
        {
            for (int index = RegisteredAgents.Count - 1; index >= 0; index--)
            {
                if (RegisteredAgents[index] == null)
                {
                    RegisteredAgents.RemoveAt(index);
                }
            }
        }

        #endregion

        #region Editor

        /// <summary>
        /// 在选中管理器时绘制游泳边界和当前目标位置。
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1, 0, 0, 0.5f);
            Gizmos.DrawCube(transform.position, swimLimits * 2f);
            Gizmos.color = new Color(0, 1, 0, 1);
            Gizmos.DrawSphere(GoalPosition, 0.2f);
        }

        #endregion
    }
}
