using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

namespace FlockingSystem.ECS
{
    /// <summary>单个 ECS 敌人的受击和死亡视觉状态。</summary>
    public struct EnemyFlockVisualState
    {
        /// <summary>当前生命百分比，范围为 0 到 1。</summary>
        public float Health01;

        /// <summary>程序化裂纹的稳定实例种子。</summary>
        public float CrackSeed;

        /// <summary>当前闪白强度，范围为 0 到 1。</summary>
        public float HitFlash;

        /// <summary>当前死亡溶解进度，范围为 0 到 1。</summary>
        public float DeathProgress;

        /// <summary>实例基础颜色。</summary>
        public float4 BaseColor;

        /// <summary>实例裂纹颜色。</summary>
        public float4 CrackColor;

        /// <summary>实例裂纹发光颜色。</summary>
        public float4 EmissionColor;
    }

    /// <summary>Entities Graphics 的生命百分比实例属性。</summary>
    [MaterialProperty("_Health01")]
    public struct EnemyFlockHealth01Property : IComponentData
    {
        public float Value;
    }

    /// <summary>Entities Graphics 的受击闪白实例属性。</summary>
    [MaterialProperty("_HitFlash")]
    public struct EnemyFlockHitFlashProperty : IComponentData
    {
        public float Value;
    }

    /// <summary>Entities Graphics 的死亡溶解进度实例属性。</summary>
    [MaterialProperty("_DeathProgress")]
    public struct EnemyFlockDeathProgressProperty : IComponentData
    {
        public float Value;
    }

    /// <summary>Entities Graphics 的程序化裂纹实例种子。</summary>
    [MaterialProperty("_CrackSeed")]
    public struct EnemyFlockCrackSeedProperty : IComponentData
    {
        public float Value;
    }

    /// <summary>Entities Graphics 的裂纹颜色实例属性。</summary>
    [MaterialProperty("_CrackColor")]
    public struct EnemyFlockCrackColorProperty : IComponentData
    {
        public float4 Value;
    }

    /// <summary>Entities Graphics 的裂纹发光颜色实例属性。</summary>
    [MaterialProperty("_EmissionColor")]
    public struct EnemyFlockEmissionColorProperty : IComponentData
    {
        public float4 Value;
    }
}
