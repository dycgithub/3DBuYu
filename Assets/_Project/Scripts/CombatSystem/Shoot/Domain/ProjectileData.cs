using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 保存投射物从攻击数据转换后的不可变快照。
    /// </summary>
    public struct ProjectileData
    {
        /// <summary>投射物实例 ID。</summary>
        public int ProjectileInstanceId;
        /// <summary>所属攻击实例 ID。</summary>
        public int AttackInstanceId;
        /// <summary>发起攻击的对象实例 ID。</summary>
        public int SourceInstanceId;
        /// <summary>投射物配置。</summary>
        public ProjectileProfileSO ProjectileProfile;
        /// <summary>投射物生成时复制的配置快照。</summary>
        public ProjectileProfileSO ProjectileProfileSnapshot;
        /// <summary>发射起点。</summary>
        public Vector3 OriginPosition;
        /// <summary>初始移动方向。</summary>
        public Vector3 LaunchDirection;
        /// <summary>投射物伤害量。</summary>
        public float DamageAmount;
        /// <summary>投射物速度。</summary>
        public float SpeedUnitsPerSecond;
        /// <summary>最大旅行距离。</summary>
        public float MaximumTravelDistance;
        /// <summary>碰撞扫掠半径。</summary>
        public float CollisionRadius;
        /// <summary>剩余穿透次数。</summary>
        public int PenetrationCount;
        /// <summary>伤害类型。</summary>
        public DamageType DamageType;
        /// <summary>指示本次攻击是否暴击。</summary>
        public bool WasCritical;
        /// <summary>本次投射物允许命中的目标阵营。</summary>
        public ProjectileTargetMode TargetMode;
    }
}
