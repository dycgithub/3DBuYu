using Interfaces;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 描述一次不带资源、阶段和冷却前置的投射物发射请求。
    /// </summary>
    public readonly struct ProjectileLaunchRequest
    {
        /// <summary>发射实体的实例标识。</summary>
        public int SourceInstanceId { get; }

        /// <summary>投射物静态配置。</summary>
        public ProjectileProfileSO ProjectileProfile { get; }

        /// <summary>投射物生成位置，使用世界坐标。</summary>
        public Vector3 OriginPosition { get; }

        /// <summary>投射物初始方向。</summary>
        public Vector3 LaunchDirection { get; }

        /// <summary>本次投射物的伤害量。</summary>
        public float DamageAmount { get; }

        /// <summary>一次请求生成的投射物数量。</summary>
        public int ProjectileCount { get; }

        /// <summary>每个投射物允许的额外穿透次数。</summary>
        public int PenetrationCount { get; }

        /// <summary>是否将本次攻击标记为暴击。</summary>
        public bool WasCritical { get; }

        /// <summary>本次投射物允许命中的目标阵营。</summary>
        public ProjectileTargetMode TargetMode { get; }

        /// <summary>创建一次直接发射请求。</summary>
        /// <param name="sourceInstanceId">发射实体的实例标识。</param>
        /// <param name="projectileProfile">投射物静态配置。</param>
        /// <param name="originPosition">发射位置，使用世界坐标。</param>
        /// <param name="launchDirection">发射方向。</param>
        /// <param name="damageAmount">本次投射物伤害量。</param>
        /// <param name="projectileCount">生成数量。</param>
        /// <param name="penetrationCount">额外穿透次数。</param>
        /// <param name="wasCritical">是否为暴击。</param>
        public ProjectileLaunchRequest(
            int sourceInstanceId,
            ProjectileProfileSO projectileProfile,
            Vector3 originPosition,
            Vector3 launchDirection,
            float damageAmount,
            int projectileCount = 1,
            int penetrationCount = 0,
            bool wasCritical = false,
            ProjectileTargetMode targetMode = ProjectileTargetMode.Enemy)
        {
            SourceInstanceId = sourceInstanceId;
            ProjectileProfile = projectileProfile;
            OriginPosition = originPosition;
            LaunchDirection = launchDirection;
            DamageAmount = damageAmount;
            ProjectileCount = projectileCount;
            PenetrationCount = penetrationCount;
            WasCritical = wasCritical;
            TargetMode = targetMode;
        }
    }
}
