using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace FlockingSystem.ECS
{
    /// <summary>
    /// FlockAgent → ECS 烘焙器。
    /// 将 FlockAgent 上的序列化字段转换为 FlockAgentData ComponentData。
    /// 无需额外挂载 Authoring 组件——Unity 自动发现 Baker 类。
    ///
    /// 烘焙时机：SubScene 构建或主场景 GameObjectConversion 时。
    /// 运行时：FlockBoidsSystem 读取这些 ComponentData 驱动鱼群行为。
    /// </summary>
    public class FlockAgentBaker : Baker<FlockAgent>
    {
        /// <summary>
        /// 将 FlockAgent MonoBehaviour 字段烘焙为 ECS Components。
        /// </summary>
        public override void Bake(FlockAgent agent)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new FlockAgentData
            {
                NeighbourDistance = agent.NeighbourDistance,
                SeparationDistance = agent.SeparationDistance,
                RotationSpeed = agent.RotationSpeed,
                Speed = UnityEngine.Random.Range(agent.MinSpeed, agent.MaxSpeed),
                SpeedMultiplier = agent.SpeedMultiplier,
                MinSpeed = agent.MinSpeed,
                MaxSpeed = agent.MaxSpeed,
            });
        }
    }

    /// <summary>
    /// FlockManager → ECS 烘焙器。
    /// 将 FlockManager 的全局参数烘焙为 FlockGoalData Singleton Component。
    /// </summary>
    public class FlockManagerBaker : Baker<FlockManager>
    {
        /// <summary>
        /// 将 FlockManager 的全局参数烘焙为 Singleton ComponentData。
        /// </summary>
        public override void Bake(FlockManager manager)
        {
            var entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new FlockGoalData
            {
                GoalPos = new float3(
                    manager.GoalPos.x, manager.GoalPos.y, manager.GoalPos.z),
                SwimCenter = new float3(
                    manager.SwimCenter.x, manager.SwimCenter.y, manager.SwimCenter.z),
                SwimLimits = new float3(
                    manager.SwimLimits.x, manager.SwimLimits.y, manager.SwimLimits.z),
            });
        }
    }
}
