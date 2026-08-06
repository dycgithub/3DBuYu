using UnityEngine;
using System.Collections.Generic;
#if UNITY_ENTITIES
using Unity.Entities;
using Unity.Mathematics;
using FlockingSystem.ECS;
#endif

namespace FlockingSystem
{
    /// <summary>
    /// 通用群游管理器 — 纯逻辑模块,与其他系统完全解耦。
    ///
    /// 管理一组 FlockAgent 的群游行为：
    /// - 随机游走目标点
    /// - 游泳边界约束
    /// - 全局速度控制
    ///
    /// 不负责生成代理 — 代理由外部系统(EnemySpawnManager/装饰鱼生成器等)
    /// 通过 <see cref="Register"/> 注册。
    /// ECS 可用时自动切换到 Burst 加速路径。
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

        /// <summary>群游趋向目标点（世界坐标）。</summary>
        public Vector3 GoalPos { get; private set; }

        /// <summary>游泳区域边界（世界坐标 AABB）。</summary>
        public Bounds SwimBounds { get; private set; }

        /// <summary>游泳区域中心（世界坐标）。</summary>
        public Vector3 SwimCenter => transform.position;

        /// <summary>游泳区域半尺寸。</summary>
        public Vector3 SwimLimits => swimLimits;

        /// <summary>所有已注册的代理列表。</summary>
        public List<FlockAgent> Agents { get; private set; }

        #endregion

        #region Unity Lifecycle

        void Awake()
        {
            Agents = new List<FlockAgent>(50);
        }

        void Start()
        {
            GoalPos = transform.position;

            if (IsECSActive())
            {
                Debug.Log("[FlockManager] ECS 模式已激活。");
            }
        }

        void Update()
        {
            SwimBounds = new Bounds(transform.position, swimLimits * 2f);

            // 随机更新目标点
            if (Random.value < goalChangeChance)
            {
                GoalPos = transform.position + new Vector3(
                    Random.Range(-swimLimits.x, swimLimits.x),
                    Random.Range(-swimLimits.y, swimLimits.y),
                    Random.Range(-swimLimits.z, swimLimits.z));
            }

#if UNITY_ENTITIES
            UpdateECSSingleton();
#endif
        }

#if UNITY_ENTITIES
        private void UpdateECSSingleton()
        {
            foreach (var world in World.All)
            {
                if (!world.IsCreated) continue;

                var system = world.GetExistingSystem<FlockBoidsSystem>();
                if (system == default) continue;

                var entityManager = world.EntityManager;
                var query = entityManager.CreateEntityQuery(
                    ComponentType.ReadWrite<FlockGoalData>());

                if (query.TryGetSingletonEntity<FlockGoalData>(out var entity))
                {
                    entityManager.SetComponentData(entity, new FlockGoalData
                    {
                        GoalPos = new float3(GoalPos.x, GoalPos.y, GoalPos.z),
                        SwimCenter = new float3(transform.position.x, transform.position.y, transform.position.z),
                        SwimLimits = new float3(swimLimits.x, swimLimits.y, swimLimits.z),
                    });
                }
            }
        }
#endif

        #endregion

        #region Public API

        /// <summary>
        /// 注册代理到此群组。
        /// </summary>
        public void Register(FlockAgent agent)
        {
            if (agent == null || Agents.Contains(agent)) return;
            Agents.Add(agent);
        }

        /// <summary>
        /// 从群组注销代理。
        /// </summary>
        public void Unregister(FlockAgent agent)
        {
            Agents.Remove(agent);
        }

        /// <summary>
        /// 批量设置所有代理的速度倍率（如冰冻/加速效果）。
        /// </summary>
        public void SetAllSpeedMultiplier(float multiplier)
        {
            foreach (var agent in Agents)
            {
                if (agent != null)
                    agent.SpeedMultiplier = multiplier;
            }

#if UNITY_ENTITIES
            foreach (var world in World.All)
            {
                if (!world.IsCreated) continue;
                var em = world.EntityManager;
                var query = em.CreateEntityQuery(
                    ComponentType.ReadWrite<FlockAgentData>());
                var agentsData = query.ToComponentDataArray<FlockAgentData>(Unity.Collections.Allocator.Temp);
                for (int i = 0; i < agentsData.Length; i++)
                {
                    var a = agentsData[i];
                    a.SpeedMultiplier = multiplier;
                    agentsData[i] = a;
                }
                em.SetComponentData(query, agentsData);
                agentsData.Dispose();
            }
#endif
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
            for (int i = Agents.Count - 1; i >= 0; i--)
            {
                if (Agents[i] == null)
                    Agents.RemoveAt(i);
            }
        }

        #endregion

        #region Utility

        private static bool IsECSActive()
        {
#if UNITY_ENTITIES
            foreach (var world in World.All)
            {
                if (world.IsCreated && world.GetExistingSystem<FlockBoidsSystem>() != default)
                    return true;
            }
#endif
            return false;
        }

        #endregion

        #region Editor

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1, 0, 0, 0.5f);
            Gizmos.DrawCube(transform.position, swimLimits * 2f);
            Gizmos.color = new Color(0, 1, 0, 1);
            Gizmos.DrawSphere(GoalPos, 0.2f);
        }

        #endregion
    }
}
