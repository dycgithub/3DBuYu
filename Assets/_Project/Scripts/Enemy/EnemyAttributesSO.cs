using CombatSystem;
using EffectSystem;
using UnityEngine;

namespace EnemySystem
{
    /// <summary>
    /// 敌人实体的静态配置。
    /// <para>该资产保存敌人的生命、战斗反馈和行为参数；速度、位置及群游算法参数由 <see cref="FlockingSystem.ECS.EnemyFlockSettingsSO"/> 提供。</para>
    /// <para>行为组件只负责执行配置，不在预制体上重复保存基础数值。</para>
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

        [Header("成长敌人")]
        [Min(0f)] public float growthConsumeRadius = 1.25f;
        [Range(0f, 1f)] public float growthConsumeChance = 0.25f;
        [Min(0f)] public float growthCheckInterval = 0.25f;

        [Header("反弹敌人")]
        [Min(0f)] public float reflectWindowSeconds = 0.05f;

        [Header("远程攻击敌人")]
        [Min(0f)] public float shooterAttackRadius = 18f;
        [Min(0f)] public float shooterFireInterval = 2f;
        [Min(0f)] public float shooterDamage = 5f;
        public ProjectileProfileSO shooterProjectileProfile;

        [Header("范围效果敌人")]
        [Min(0f)] public float areaEffectRadius = 5f;
        [Min(0f)] public float areaEffectTickInterval = 0.2f;
        [Range(0f, 1f)] public float areaSlowMultiplier = 0.5f;
        [Min(0f)] public float areaHealPerSecond = 10f;
        public EnemyAreaEffectType areaEffectType = EnemyAreaEffectType.Slow;
        public BuffDefinitionSO areaSlowBuff;

        [Header("子母敌人")]
        public Enemy childPrefab;
        [Min(0f)] public float childSpawnInterval = 5f;
        [Min(0)] public int childMaximumAlive = 5;
        [Min(0f)] public float childSpawnRadius = 1.5f;
        [Min(0f)] public float childHealthMultiplier = 1f;
        [Min(0f)] public float childSpeedMultiplier = 1f;

        [Header("爆炸敌人")]
        [Min(0f)] public float explosionDamage = 15f;
    }
}
