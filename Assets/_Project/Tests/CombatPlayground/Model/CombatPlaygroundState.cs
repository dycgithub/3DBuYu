using UnityEngine;

namespace _Project.Tests.CombatPlayground.Model
{
    /// <summary>实验场景可手动触发的表现阶段。</summary>
    public enum CombatPlaygroundStage
    {
        Charge,
        BeforeFire,
        AfterFire,
        Hit,
        Expired
    }

    /// <summary>
    /// 保存 CombatPlayground 的会话内攻击和表现覆盖值。
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
        [SerializeField, Range(0f, 1f)] private float scalePunch = 0.35f;
        [SerializeField, Min(0.01f)] private float punchDuration = 0.24f;
        [SerializeField] private Color flashColor = new(1f, 0.38f, 0.16f, 1f);
        [SerializeField, Min(0f)] private float flashDuration = 0.16f;

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

        /// <summary>当前缩放脉冲强度。</summary>
        public float ScalePunch => scalePunch;

        /// <summary>当前缩放脉冲时长。</summary>
        public float PunchDuration => punchDuration;

        /// <summary>当前命中闪烁颜色。</summary>
        public Color FlashColor => flashColor;

        /// <summary>当前命中闪烁时长。</summary>
        public float FlashDuration => flashDuration;

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

        /// <summary>更新蓄力表现时长。</summary>
        public void SetChargeDuration(float value) => chargeDuration = Mathf.Max(0f, value);

        /// <summary>更新缩放脉冲强度。</summary>
        public void SetScalePunch(float value) => scalePunch = Mathf.Clamp01(value);

        /// <summary>更新缩放脉冲时长。</summary>
        public void SetPunchDuration(float value) => punchDuration = Mathf.Max(0.01f, value);

        /// <summary>更新命中闪烁颜色。</summary>
        public void SetFlashColor(Color value) => flashColor = value;

        /// <summary>更新命中闪烁时长。</summary>
        public void SetFlashDuration(float value) => flashDuration = Mathf.Max(0f, value);
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
        public int ShotCount { get; }
        public int SkillCount { get; }
        public string ProjectileName { get; }
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
            int shotCount,
            int skillCount,
            string projectileName,
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
            ShotCount = shotCount;
            SkillCount = skillCount;
            ProjectileName = projectileName;
            BuffName = buffName;
            Status = status;
            EventLog = eventLog;
        }
    }
}
