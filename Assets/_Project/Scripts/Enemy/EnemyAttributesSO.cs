using CombatSystem;
using EffectSystem;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// 敌人实体的静态配置。
    /// <para>该资产保存敌人的生命、战斗反馈和行为参数；速度、位置及群游算法参数由 <see cref="FlockingSystem.ECS.EnemyFlockSettingsSO"/> 提供。</para>
    /// <para>范围精英战术参数只驱动状态机；范围半径、周期和 Buff 引用仍决定实际效果。</para>
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyAttributes", menuName = "Enemy/Enemy Attributes")]
    public class EnemyAttributesSO : ScriptableObject
    {
        [Header("基础属性")]
        [Tooltip("敌人设计编号。")]
        [Min(1)]
        public int enemyId = 1;

        [Tooltip("最大生命值，难度倍率作用前的基础值。")]
        [Min(0f)]
        public float baseHealth = 100f;

        [Tooltip("敌人类型，用于锁定分类和读取 Flocking Profile。")]
        public EnemyType enemyType = EnemyType.Normal;

        [Header("奖励")]
        [Tooltip("死亡时是否参与奖励和击杀统计。")]
        public EnemyRewardMode rewardMode = EnemyRewardMode.Award;

        [Header("受击与死亡反馈")]
        [Tooltip("受到有效伤害时播放的效果。")]
        public EffectId hitEffect = EffectId.EnemyHit;

        [Tooltip("进入死亡状态时播放的效果。")]
        public EffectId deathEffect = EffectId.EnemyDeath;

        [Tooltip("共享材质使用的视觉参数；为空时使用组件默认值。")]
        public EnemySystem.Visual.EnemyVisualConfigSO visualConfig;

        [Header("闪避")]
        [Tooltip("闪避概率，范围为 0 到 1。")]
        [Range(0f, 1f)]
        public float dodgeChance;

        [Tooltip("闪避成功时的水平位移距离，单位为世界单位。")]
        [Min(0f)]
        public float dodgeDistance = 2f;

        [Header("护盾")]
        [Tooltip("护盾最大值。")]
        [Min(0f)]
        public float shieldValue;

        [Tooltip("护盾恢复速度，单位为点/秒。")]
        [Min(0f)]
        public float shieldRegenRate;

        [Tooltip("受击后开始恢复护盾前等待的时间，单位为秒。")]
        [Min(0f)]
        public float shieldRegenDelay;

        [Header("死亡爆炸")]
        [Tooltip("死亡爆炸检测半径，单位为世界单位。")]
        [Min(0f)]
        public float explosionRadius;

        [Header("死亡奖励")]
        [Tooltip("死亡时回复的能量。")]
        [Min(0f)]
        public float energyReward;

        [Tooltip("死亡时提供的基础积分。")]
        [Min(0)]
        public int pointsReward;

        [Header("进攻敌人")]
        [Min(0f)] public float assaultPerceptionRadius = 12f;
        [Min(0f)] public float assaultTurnSpeed = 360f;
        [Min(0f)] public float assaultTurnTolerance = 2f;
        [Min(0f)] public float assaultChargeDuration = 1f;
        [Min(0f)] public float assaultDashSpeed = 18f;
        [Min(0f)] public float assaultDashDistance = 8f;
        [Tooltip("Boss 第二阶段连续执行的冲锋次数。")]
        [Min(1)] public int assaultPhaseTwoDashCount = 2;
        [Tooltip("冲锋动作完成后的恢复时间。")]
        [Min(0f)] public float assaultAttackRecoveryDuration = 1f;

        [Header("吞噬 GOAP")]
        [Tooltip("两次 GOAP 决策之间的最短间隔。")]
        [Min(0f)] public float goapReplanInterval = 0.25f;
        [Tooltip("Growth 查询可吞噬目标的半径。")]
        [Min(0f)] public float growthTargetSearchRadius = 12f;
        [Tooltip("Growth 与可吞噬目标的接近距离。")]
        [Min(0f)] public float growthAbsorbRange = 1.5f;
        [Tooltip("Growth 接近玩家并造成接触伤害的距离。")]
        [Min(0f)] public float growthPlayerEngageRange = 1.5f;

        [Header("反弹敌人")]
        [Min(0f)] public float reflectWindowSeconds = 0.05f;

        [Header("远程攻击敌人")]
        [Min(0f)] public float shooterAttackRadius = 18f;
        [Min(0f)] public float shooterFireInterval = 2f;
        [Min(0f)] public float shooterDamage = 5f;
        [Tooltip("玩家进入此距离时 Shooter 开始拉开距离。")]
        [Min(0f)] public float shooterMinimumRange = 4f;
        [Tooltip("第二阶段使用的射击间隔。")]
        [Min(0f)] public float shooterPhaseTwoFireInterval = 1f;
        public ProjectileProfileSO shooterProjectileProfile;

        [Header("范围效果敌人")]
        [Min(0f)] public float areaEffectRadius = 5f;
        [Min(0f)] public float areaEffectTickInterval = 0.2f;
        [Range(0f, 1f)] public float areaSlowMultiplier = 0.5f;
        public EnemyAreaEffectType areaEffectType = EnemyAreaEffectType.Slow;
        public BuffDefinitionSO areaSlowBuff;
        public BuffDefinitionSO areaHealBuff;

        [Header("范围精英战术")]
        [Tooltip("范围精英搜索玩家或受伤盟友的半径。")]
        [Min(0f)] public float areaSensingRadius = 15f;
        [Tooltip("健康时与玩家保持的首选安全距离，也作为撤退距离的起点。")]
        [Min(0f)] public float areaPreferredDistance = 8f;
        [Tooltip("玩家低于此距离时优先撤退。")]
        [Min(0f)] public float areaMinimumSafeDistance = 4f;
        [Tooltip("生命值降低时撤退距离的上限。")]
        [Min(0f)] public float areaMaximumRetreatDistance = 16f;
        [Tooltip("生命比例不高于此值时触发撤退。")]
        [Range(0f, 1f)] public float areaLowHealthRetreatThreshold = 0.3f;
        [Tooltip("接近施法目标的基础移动速度，难度倍率作用前。")]
        [Min(0f)] public float areaApproachSpeed = 2f;
        [Tooltip("远离玩家的基础移动速度，难度倍率作用前。")]
        [Min(0f)] public float areaRetreatSpeed = 2.5f;
        [Tooltip("范围精英的最大转向速度，单位为度/秒。")]
        [Min(0f)] public float areaTurnSpeed = 180f;
        [Tooltip("两次完整目标搜索之间的秒数。")]
        [Min(0.01f)] public float areaSensingInterval = 0.3f;
        [Tooltip("每次持续释放范围效果的施法窗口时长。")]
        [Min(0f)] public float areaCastWindowSeconds = 0.8f;
        [Tooltip("两次施法窗口之间的冷却时长。")]
        [Min(0f)] public float areaCastCooldownSeconds = 2.5f;

        [Header("子母敌人")]
        public Enemy childPrefab;
        [Min(0f)] public float childSpawnInterval = 5f;
        [Min(0)] public int childMaximumAlive = 5;
        [Min(0f)] public float childSpawnRadius = 1.5f;
        [Min(0f)] public float childHealthMultiplier = 1f;
        [Min(0f)] public float childSpeedMultiplier = 1f;
        [Tooltip("第一阶段期望维持的存活子敌人数。")]
        [Min(0)] public int childPhaseOneTargetAlive = 2;
        [Tooltip("第二阶段期望维持的存活子敌人数。")]
        [Min(0)] public int childPhaseTwoTargetAlive = 5;
        [Tooltip("第二阶段召唤子敌人的间隔。")]
        [Min(0f)] public float childPhaseTwoSpawnInterval = 2.5f;
        [Tooltip("Mother 接近到此距离后维持接触攻击。")]
        [Min(0f)] public float motherPlayerEngageRange = 1.5f;

        [Header("Boss 阶段")]
        [Tooltip("生命值低于此比例时进入第二阶段。")]
        [Range(0f, 1f)] public float bossSecondPhaseHealthPercent = 0.5f;
        [Tooltip("Boss 第二阶段的定向移动速度倍率。")]
        [Min(1f)] public float bossPhaseTwoSpeedMultiplier = 1.25f;
        [Tooltip("Boss 实例的基础锁定威胁值。")]
        [Range(0f, 100f)] public float bossThreatLevel = 80f;

        [Header("爆炸敌人")]
        [Min(0f)] public float explosionDamage = 15f;
    }
}
