using System.Collections.Generic;
using UnityEngine;
using EffectSystem;
using UnityEngine.Serialization;

namespace CombatSystem
{
    /// <summary>
    /// 描述投射物攻击的静态配置，普通投射物和 Hitscan 共用此配置入口。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Attack Projectile Profile")]
    public sealed class ProjectileProfileSO : ScriptableObject
    {
        /// <summary>生成一次攻击所需的能量。</summary>
        [Min(0f)] public float EnergyCost = 1f;

        /// <summary>投射物速度，单位为世界单位/秒。</summary>
        [FormerlySerializedAs("Speed")]
        public float SpeedUnitsPerSecond = 15f;

        /// <summary>投射物最大旅行距离，单位为世界单位。</summary>
        [FormerlySerializedAs("MaxDistance")]
        public float MaximumTravelDistance = 50f;

        /// <summary>投射物碰撞扫掠半径，单位为世界单位。</summary>
        [FormerlySerializedAs("Radius")]
        public float CollisionRadius = 0.1f;

        /// <summary>伤害类型。</summary>
        public DamageType DamageType = DamageType.Physical;

        /// <summary>投射物轨迹配置；为空时使用直线轨迹。</summary>
        [FormerlySerializedAs("Trajectory")]
        public TrajectoryDefinitionSO TrajectoryDefinition;

        /// <summary>投射物视觉配置；Hitscan 可以不配置视觉对象。</summary>
        [FormerlySerializedAs("Visual")]
        public ProjectileVisualDefinitionSO VisualDefinition;

        /// <summary>没有自定义命中效果时播放的默认效果标识。</summary>
        [FormerlySerializedAs("HitEffect")]
        public EffectId HitEffectId = EffectId.BulletHit;

        /// <summary>没有自定义过期效果时播放的默认效果标识。</summary>
        [FormerlySerializedAs("ExpiredEffect")]
        public EffectId ExpiredEffectId = EffectId.BulletExpired;

        /// <summary>伤害成功后按顺序执行的自定义效果。</summary>
        [FormerlySerializedAs("OnHitEffects")]
        public List<ProjectileEffectDefinitionSO> OnHitEffectDefinitions;

        /// <summary>投射物过期时按顺序执行的自定义效果。</summary>
        [FormerlySerializedAs("OnExpiredEffects")]
        public List<ProjectileEffectDefinitionSO> OnExpiredEffectDefinitions;

        /// <summary>伤害提交前按顺序执行的自定义效果。</summary>
        [FormerlySerializedAs("OnTriggerEffects")]
        public List<ProjectileEffectDefinitionSO> BeforeHitEffectDefinitions;

        /// <summary>
        /// 创建未保存到工程的运行时投射物配置。
        /// </summary>
        /// <param name="profileName">运行时对象名称；空值使用默认名称。</param>
        /// <returns>新创建的投射物配置。</returns>
        /// <remarks>调用方负责临时 ScriptableObject 的生命周期。</remarks>
        public static ProjectileProfileSO CreateRuntimeProfile(string profileName)
        {
            ProjectileProfileSO profile = CreateInstance<ProjectileProfileSO>();
            profile.name = string.IsNullOrEmpty(profileName) ? "RuntimeProjectileProfile" : profileName;
            return profile;
        }
    }
}
