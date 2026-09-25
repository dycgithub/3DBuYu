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

        /// <summary>批量发射时每颗子弹相对瞄准方向的最大偏转角；零表示平行发射。</summary>
        [Range(0f, 180f)] public float ScatterAngleDegrees;

        /// <summary>每颗投射物的一次性速度随机比例；零表示固定速度。</summary>
        [Range(0f, 0.5f)] public float RandomSpeedVariance;

        /// <summary>是否将批次投射物按一次攻击间隔依次发射。</summary>
        public bool EmitProjectilesSequentially;

        /// <summary>顺序发射批次的固定弹数；零表示使用攻击请求中的数量。</summary>
        [Min(0)] public int SequentialProjectileCount;

        /// <summary>每发相对瞄准方向的随机偏转上限。</summary>
        [Range(0f, 180f)] public float RandomDirectionAngleDegrees;

        /// <summary>生成时应用一次的弹速倍率。</summary>
        [Min(0f)] public float ProjectileSpeedMultiplier = 1f;

        /// <summary>目标生命比例低于阈值时的伤害倍率。</summary>
        [Range(0f, 1f)] public float ExecutionHealthThreshold;

        /// <summary>满足斩杀条件时应用的伤害倍率。</summary>
        [Min(1f)] public float ExecutionDamageMultiplier = 1f;

        /// <summary>连续命中同一目标时每层增加的伤害比例。</summary>
        [Range(0f, 1f)] public float ComboDamageBonusPerHit;

        /// <summary>连续命中效果最多增加的层数。</summary>
        [Min(0)] public int ComboMaximumStacks;

        /// <summary>连续命中层数的失效时间。</summary>
        [Min(0f)] public float ComboResetSeconds;

        /// <summary>一次攻击击杀至少一个敌人时返还的能量比例。</summary>
        [Range(0f, 1f)] public float KillEnergyRefundRatio;

        /// <summary>命中后可跳转到其他目标的次数；零表示不跳弹。</summary>
        [Min(0)] public int RicochetCount;

        /// <summary>跳弹搜索半径。</summary>
        [Min(0f)] public float RicochetSearchRadius = 8f;

        /// <summary>每次跳弹后保留的伤害比例。</summary>
        [Range(0f, 1f)] public float RicochetDamageMultiplier = 0.75f;

        /// <summary>命中后分裂出的子弹数量；零表示不分裂。</summary>
        [Min(0)] public int SplitProjectileCount;

        /// <summary>没有可追踪目标时，分裂子弹相对入射方向的最大偏转角。</summary>
        [Range(0f, 180f)] public float SplitAngleDegrees = 30f;

        /// <summary>分裂时查找附近目标的半径。</summary>
        [Min(0f)] public float SplitTargetSearchRadius = 8f;

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
