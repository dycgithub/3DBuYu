using UnityEngine;

namespace _Project.Tests.CombatPlayground.Model
{
    /// <summary>选择技能卡的真实释放流程或隔离动作执行流程。</summary>
    public enum CombatPlaygroundSkillExecutionMode
    {
        FormalRelease,
        DirectAction
    }

    /// <summary>表示发射器攻击是否被正式攻击链接受。</summary>
    public enum CombatPlaygroundFireResult
    {
        Accepted,
        Rejected
    }

    /// <summary>选择是否开启实验场景的自动发射。</summary>
    public enum CombatPlaygroundAutoFireState
    {
        Disabled,
        Enabled
    }

    /// <summary>控制预设切换是否更新界面状态提示。</summary>
    public enum CombatPlaygroundStatusUpdateMode
    {
        Silent,
        Report
    }

    /// <summary>选择当前实验场景要调整的发射器或投射物参数。</summary>
    public enum CombatPlaygroundAttackParameter
    {
        BaseDamage,
        AttackRate,
        Range,
        ProjectileCount,
        Penetration,
        ProjectileSpeed,
        CollisionRadius
    }

    /// <summary>选择敌人临时属性面板中要编辑的参数。</summary>
    public enum CombatPlaygroundEnemyAttribute
    {
        HealthMultiplier,
        MovementMultiplier,
        DamageTakenMultiplier,
        AttackDamageMultiplier,
        AttackAvailabilityMultiplier,
        BuffDuration
    }

    /// <summary>保存当前实验敌人的会话属性调节值。</summary>
    public readonly struct CombatPlaygroundEnemyTuningSnapshot
    {
        public float HealthMultiplier { get; }
        public float MovementMultiplier { get; }
        public float DamageTakenMultiplier { get; }
        public float AttackDamageMultiplier { get; }
        public float AttackAvailabilityMultiplier { get; }
        public float BuffDuration { get; }

        /// <summary>创建敌人临时属性面板的只读快照。</summary>
        public CombatPlaygroundEnemyTuningSnapshot(
            float healthMultiplier,
            float movementMultiplier,
            float damageTakenMultiplier,
            float attackDamageMultiplier,
            float attackAvailabilityMultiplier,
            float buffDuration)
        {
            HealthMultiplier = healthMultiplier;
            MovementMultiplier = movementMultiplier;
            DamageTakenMultiplier = damageTakenMultiplier;
            AttackDamageMultiplier = attackDamageMultiplier;
            AttackAvailabilityMultiplier = attackAvailabilityMultiplier;
            BuffDuration = buffDuration;
        }
    }

    /// <summary>
    /// 保存 CombatPlayground 的会话内发射器参数。
    /// </summary>
    [System.Serializable]
    public sealed class CombatPlaygroundTuning
    {
        [SerializeField, Min(0f)] private float baseDamage = 10f;
        [SerializeField, Min(0.001f)] private float attacksPerSecond = 2f;
        [SerializeField, Min(0f)] private float baseRange = 50f;
        [SerializeField, Min(1)] private int projectileCount = 1;
        [SerializeField, Min(0)] private int penetration;
        [SerializeField, Min(0f)] private float chargeDuration = 0.08f;

        /// <summary>当前攻击基础伤害。</summary>
        public float BaseDamage => baseDamage;

        /// <summary>当前每秒攻击次数。</summary>
        public float AttacksPerSecond => attacksPerSecond;

        /// <summary>当前攻击基础射程。</summary>
        public float BaseRange => baseRange;

        /// <summary>当前一次攻击的投射物数量。</summary>
        public int ProjectileCount => projectileCount;

        /// <summary>当前一次攻击的穿透次数。</summary>
        public int Penetration => penetration;

        /// <summary>当前蓄力表现时长。</summary>
        public float ChargeDuration => chargeDuration;

        /// <summary>更新发射器基础伤害。</summary>
        public void SetBaseDamage(float value) => baseDamage = Mathf.Max(0f, value);

        /// <summary>更新发射器攻击频率。</summary>
        public void SetAttacksPerSecond(float value) => attacksPerSecond = Mathf.Max(0.001f, value);

        /// <summary>更新发射器基础射程。</summary>
        public void SetBaseRange(float value) => baseRange = Mathf.Max(0f, value);

        /// <summary>更新一次攻击的投射物数量。</summary>
        public void SetProjectileCount(float value) => projectileCount = Mathf.Max(1, Mathf.RoundToInt(value));

        /// <summary>更新一次攻击的穿透次数。</summary>
        public void SetPenetration(float value) => penetration = Mathf.Max(0, Mathf.RoundToInt(value));


    }

    /// <summary>
    /// 提供 UI 刷新所需的只读场景状态快照。
    /// </summary>
    public readonly struct CombatPlaygroundSnapshot
    {
        public string TargetName { get; }
        public float TargetHealth { get; }
        public float TargetMaxHealth { get; }
        public float MovementMultiplier { get; }
        public float Energy { get; }
        public float MaximumEnergy { get; }
        public bool HasTarget { get; }
        public bool AutoFire { get; }
        public CombatPlaygroundSkillExecutionMode SkillExecutionMode { get; }
        public CombatPlaygroundEnemyTuningSnapshot EnemyTuning { get; }
        public int ShotCount { get; }
        public int SkillCount { get; }
        public string ProjectileName { get; }
        public float ProjectileSpeed { get; }
        public float CollisionRadius { get; }
        public string BuffName { get; }
        public string Status { get; }
        public string EventLog { get; }

        /// <summary>创建 UI 所需的场景快照。</summary>
        public CombatPlaygroundSnapshot(
            string targetName,
            float targetHealth,
            float targetMaxHealth,
            float movementMultiplier,
            float energy,
            float maximumEnergy,
            bool hasTarget,
            bool autoFire,
            CombatPlaygroundSkillExecutionMode skillExecutionMode,
            CombatPlaygroundEnemyTuningSnapshot enemyTuning,
            int shotCount,
            int skillCount,
            string projectileName,
            float projectileSpeed,
            float collisionRadius,
            string buffName,
            string status,
            string eventLog)
        {
            TargetName = targetName;
            TargetHealth = targetHealth;
            TargetMaxHealth = targetMaxHealth;
            MovementMultiplier = movementMultiplier;
            Energy = energy;
            MaximumEnergy = maximumEnergy;
            HasTarget = hasTarget;
            AutoFire = autoFire;
            SkillExecutionMode = skillExecutionMode;
            EnemyTuning = enemyTuning;
            ShotCount = shotCount;
            SkillCount = skillCount;
            ProjectileName = projectileName;
            ProjectileSpeed = projectileSpeed;
            CollisionRadius = collisionRadius;
            BuffName = buffName;
            Status = status;
            EventLog = eventLog;
        }
    }
}
