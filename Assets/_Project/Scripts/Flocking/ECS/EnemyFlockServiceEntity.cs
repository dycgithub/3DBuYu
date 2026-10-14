using EnemySystem;
using GeometricMotion;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace FlockingSystem.ECS
{
    /// <summary>创建 ECS 原型并初始化单个代理的群游姿态和移动所有权。</summary>
    public sealed partial class EnemyFlockRuntimeService
    {
        /// <summary>创建只包含完整组件布局的实体原型；具体视觉和状态由实例覆盖。</summary>
        private Entity CreateAgentPrototype(EnemyFlockVisual visual)
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
            _entityManager.AddComponentData(
                entity,
                new EnemyFlockBridgeIndex { FlockSlot = -1 });
            _entityManager.AddComponentData(entity, new EnemyFlockActive());
            _entityManager.AddChunkComponentData<EnemyFlockChunkGoal>(entity);
            _entityManager.SetChunkComponentData(_entityManager.GetChunk(entity), new EnemyFlockChunkGoal
            {
                GoalPosition = new float3(
                    _flockSettings.SwimCenter.x,
                    _flockSettings.SwimCenter.y,
                    _flockSettings.SwimCenter.z),
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
            _entityManager.AddComponentData(
                entity,
                new URPMaterialPropertyBaseColor { Value = visual.BaseColor });
            _entityManager.AddComponentData(
                entity,
                new EnemyFlockHealth01Property { Value = 1f });
            _entityManager.AddComponentData(
                entity,
                new EnemyFlockHitFlashProperty { Value = 0f });
            _entityManager.AddComponentData(
                entity,
                new EnemyFlockDeathProgressProperty { Value = 0f });
            _entityManager.AddComponentData(
                entity,
                new EnemyFlockCrackSeedProperty { Value = 0f });
            _entityManager.AddComponentData(
                entity,
                new EnemyFlockCrackColorProperty { Value = visual.CrackColor });
            _entityManager.AddComponentData(
                entity,
                new EnemyFlockEmissionColorProperty { Value = visual.EmissionColor });
            GeometricMotionEntitySetup.AddComponents(
                _entityManager,
                entity,
                GetGeometricMotionConfig(),
                visual.PostTransform.Value);
            _entityManager.SetComponentEnabled<EnemyFlockActive>(entity, false);
            _entityManager.SetComponentEnabled<MaterialMeshInfo>(entity, false);
            return entity;
        }

        /// <summary>写入实体初始姿态、运动参数、空间槽位和视觉组件。</summary>
        private void SetEntityFlockData(
            EnemyFlockBridge bridge,
            EnemyFlockProfile profile,
            EnemyFlockVisual visual,
            Vector3 position,
            Quaternion rotation)
        {
            float3 entityPosition = new float3(position.x, position.y, position.z);
            quaternion entityRotation = new quaternion(rotation.x, rotation.y, rotation.z, rotation.w);
            ForgetPendingMotion(bridge);
            bridge.SetMotionMode(EnemyFlockMotionMode.Flocking);
            float seedRange = Mathf.Max(0.01f, profile.MaxSpeed - profile.MinSpeed);
            float speed = profile.MinSpeed + Mathf.Abs(
                (bridge.Slot * InitialSpeedSeedStep) % seedRange);
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
            EnemyFlockAgent agent = profile.CreateAgentData(speed);
            agent.MotionMode = EnemyFlockMotionMode.Flocking;
            agent.DirectedRotation = entityRotation;
            agent.Velocity = math.mul(
                entityRotation,
                new float3(0f, 0f, speed));
            _entityManager.SetComponentData(bridge.Entity, agent);
            _entityManager.SetComponentData(bridge.Entity, new EnemyFlockBridgeIndex
            {
                FlockSlot = bridge.Slot,
                SpatialSlot = bridge.GetComponent<Enemy>()?.SpatialSlot ?? -1,
            });
            GeometricMotionEntitySetup.Reset(
                _entityManager,
                bridge.Entity,
                visual.PostTransform.Value);
            _entityManager.SetComponentData(
                bridge.Entity,
                new MaterialMeshInfo(visual.MaterialId, visual.MeshId));
            _entityManager.SetComponentData(
                bridge.Entity,
                new URPMaterialPropertyBaseColor { Value = visual.BaseColor });
            _entityManager.SetComponentData(
                bridge.Entity,
                new EnemyFlockHealth01Property { Value = 1f });
            _entityManager.SetComponentData(
                bridge.Entity,
                new EnemyFlockHitFlashProperty { Value = 0f });
            _entityManager.SetComponentData(
                bridge.Entity,
                new EnemyFlockDeathProgressProperty { Value = 0f });
            _entityManager.SetComponentData(
                bridge.Entity,
                new EnemyFlockCrackSeedProperty { Value = GetVisualSeed(bridge) });
            _entityManager.SetComponentData(
                bridge.Entity,
                new EnemyFlockCrackColorProperty { Value = visual.CrackColor });
            _entityManager.SetComponentData(
                bridge.Entity,
                new EnemyFlockEmissionColorProperty { Value = visual.EmissionColor });
            _entityManager.SetComponentEnabled<EnemyFlockActive>(bridge.Entity, true);
            _entityManager.SetComponentEnabled<MaterialMeshInfo>(bridge.Entity, true);
        }

        /// <summary>读取当前 Flocking 世界使用的几何运动配置。</summary>
        private GeometricMotionConfig GetGeometricMotionConfig()
        {
            return _flockSettings.GeometricMotionProfile != null
                ? _flockSettings.GeometricMotionProfile.CreateConfig()
                : GeometricMotionConfig.Default;
        }

        /// <summary>为同一个对象池实例生成稳定的裂纹种子。</summary>
        private static float GetVisualSeed(EnemyFlockBridge bridge)
        {
            unchecked
            {
                uint value = (uint)(bridge.GetInstanceID() * 747796405);
                value ^= value >> 16;
                return (value & 0x00ffffffu) / 16777215f;
            }
        }
    }
}
