using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 描述发射器端口提交给攻击服务的基础攻击输入。
    /// <para>基础攻击数值由 <see cref="Play.TransmitterSO"/> 提供；装备修正由后续构建阶段叠加。</para>
    /// </summary>
    public readonly struct TransmitterAttackInput
    {
        /// <summary>发起攻击的对象实例 ID。</summary>
        public int SourceInstanceId { get; }

        /// <summary>发射器端口索引。</summary>
        public int TransmitterPortIndex { get; }

        /// <summary>发射器稳定标识。</summary>
        public string PortId { get; }

        /// <summary>本次攻击使用的子弹配置。</summary>
        public ProjectileProfileSO ProjectileProfile { get; }

        /// <summary>发射点世界坐标。</summary>
        public Vector3 OriginPosition { get; }

        /// <summary>发射方向。</summary>
        public Vector3 LaunchDirection { get; }

        /// <summary>发射器提供的基础伤害。</summary>
        public float BaseDamage { get; }

        /// <summary>发射器提供的基础最大射程，单位为世界单位。</summary>
        public float BaseRange { get; }

        /// <summary>发射器提供的每秒基础攻击次数。</summary>
        public float AttacksPerSecond { get; }

        /// <summary>发射器提供的一次攻击基础投射物数量。</summary>
        public int RequestedProjectileCount { get; }

        /// <summary>发射器提供的一次攻击基础穿透次数。</summary>
        public int RequestedPenetrationCount { get; }

        /// <summary>发射器提供的基础暴击概率。</summary>
        public float CriticalHitChance { get; }

        /// <summary>发射器提供的基础暴击伤害倍率。</summary>
        public float CriticalHitDamageMultiplier { get; }

        /// <summary>创建发射器攻击输入。</summary>
        /// <param name="sourceInstanceId">发起攻击的对象实例 ID。</param>
        /// <param name="transmitterPortIndex">发射器端口索引。</param>
        /// <param name="projectileProfile">本次攻击使用的子弹配置。</param>
        /// <param name="originPosition">发射点世界坐标。</param>
        /// <param name="launchDirection">发射方向。</param>
        /// <param name="baseDamage">发射器基础伤害。</param>
        /// <param name="baseRange">发射器基础最大射程，单位为世界单位。</param>
        /// <param name="attacksPerSecond">发射器每秒基础攻击次数。</param>
        /// <param name="requestedProjectileCount">一次攻击基础投射物数量。</param>
        /// <param name="requestedPenetrationCount">一次攻击基础穿透次数。</param>
        /// <param name="criticalHitChance">发射器基础暴击概率。</param>
        /// <param name="criticalHitDamageMultiplier">发射器基础暴击伤害倍率。</param>
        public TransmitterAttackInput(
            int sourceInstanceId,
            int transmitterPortIndex,
            ProjectileProfileSO projectileProfile,
            Vector3 originPosition,
            Vector3 launchDirection,
            float baseDamage,
            float baseRange,
            float attacksPerSecond,
            int requestedProjectileCount,
            int requestedPenetrationCount,
            float criticalHitChance,
            float criticalHitDamageMultiplier,
            string portId = null)
        {
            SourceInstanceId = sourceInstanceId;
            TransmitterPortIndex = transmitterPortIndex;
            PortId = portId;
            ProjectileProfile = projectileProfile;
            OriginPosition = originPosition;
            LaunchDirection = launchDirection;
            BaseDamage = baseDamage;
            BaseRange = baseRange;
            AttacksPerSecond = attacksPerSecond;
            RequestedProjectileCount = requestedProjectileCount;
            RequestedPenetrationCount = requestedPenetrationCount;
            CriticalHitChance = criticalHitChance;
            CriticalHitDamageMultiplier = criticalHitDamageMultiplier;
        }
    }
}
