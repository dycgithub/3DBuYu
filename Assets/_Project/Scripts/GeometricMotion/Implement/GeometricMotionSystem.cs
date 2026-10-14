using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GeometricMotion
{
    /// <summary>
    /// 在 ECS Transform 计算前更新几何体的局部形变矩阵。
    /// <para>系统只负责表现矩阵，不写入实体位置和移动旋转。</para>
    /// </summary>
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct GeometricMotionSystem : ISystem
    {
        /// <summary>创建通用几何运动表现 Job。</summary>
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<GeometricMotionConfig>();
        }

        /// <summary>并行推进所有已经初始化几何运动组件的实体。</summary>
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency = new GeometricMotionJob().ScheduleParallel(state.Dependency);
        }

        /// <summary>读取运动输入并写入当前实体的 PostTransformMatrix。</summary>
        [BurstCompile]
        private partial struct GeometricMotionJob : IJobEntity
        {
            /// <summary>推进一个实体的形变阶段并消费当前帧信号。</summary>
            private void Execute(
                ref GeometricMotionState motionState,
                in GeometricMotionConfig config,
                in GeometricMotionInput input,
                ref GeometricMotionSignal signal,
                in GeometricMotionBaseTransform baseTransform,
                ref PostTransformMatrix postTransform)
            {
                GeometricMotionOutput output = GeometricMotionMath.Evaluate(
                    ref motionState,
                    config,
                    input,
                    signal);
                float4x4 deformation = float4x4.TRS(
                    output.LocalOffset,
                    output.LocalRotation,
                    output.Scale);
                postTransform.Value = math.mul(baseTransform.Value, deformation);
                signal = default;
            }
        }
    }

    /// <summary>为任意 ECS 实体添加并初始化通用几何运动组件。</summary>
    public static class GeometricMotionEntitySetup
    {
        /// <summary>
        /// 添加表现所需组件，并保存未形变的源模型矩阵。
        /// </summary>
        /// <param name="entityManager">拥有目标实体的 EntityManager。</param>
        /// <param name="entity">要初始化的实体。</param>
        /// <param name="config">几何运动配置。</param>
        /// <param name="baseTransform">未叠加形变的模型局部矩阵。</param>
        public static void AddComponents(
            EntityManager entityManager,
            Entity entity,
            GeometricMotionConfig config,
            float4x4 baseTransform)
        {
            entityManager.AddComponentData(entity, config.Normalize());
            entityManager.AddComponentData(entity, new GeometricMotionInput());
            entityManager.AddComponentData(entity, new GeometricMotionSignal());
            entityManager.AddComponentData(entity, new GeometricMotionState());
            entityManager.AddComponentData(entity, new GeometricMotionBaseTransform
            {
                Value = baseTransform,
            });
            entityManager.AddComponentData(entity, new PostTransformMatrix
            {
                Value = baseTransform,
            });
        }

        /// <summary>更新实体的基础模型矩阵并清除上一帧形变。</summary>
        /// <param name="entityManager">拥有目标实体的 EntityManager。</param>
        /// <param name="entity">要更新的实体。</param>
        /// <param name="baseTransform">新的未形变模型局部矩阵。</param>
        public static void SetBaseTransform(
            EntityManager entityManager,
            Entity entity,
            float4x4 baseTransform)
        {
            entityManager.SetComponentData(entity, new GeometricMotionBaseTransform
            {
                Value = baseTransform,
            });
            entityManager.SetComponentData(entity, new PostTransformMatrix
            {
                Value = baseTransform,
            });
        }

        /// <summary>重置实体的阶段、跟随量、信号和当前表现矩阵。</summary>
        /// <param name="entityManager">拥有目标实体的 EntityManager。</param>
        /// <param name="entity">要重置的实体。</param>
        /// <param name="baseTransform">未形变模型局部矩阵。</param>
        public static void Reset(
            EntityManager entityManager,
            Entity entity,
            float4x4 baseTransform)
        {
            entityManager.SetComponentData(entity, new GeometricMotionState());
            entityManager.SetComponentData(entity, new GeometricMotionInput());
            entityManager.SetComponentData(entity, new GeometricMotionSignal());
            SetBaseTransform(entityManager, entity, baseTransform);
        }
    }
}
