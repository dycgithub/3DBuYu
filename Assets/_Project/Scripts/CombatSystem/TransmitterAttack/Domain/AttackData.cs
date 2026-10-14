using UnityEngine;
using Interfaces;

namespace CombatSystem
{
    /// <summary>
    /// 保存攻击构建器生成、等待执行器提交的攻击数据和投射物生成偏移。
    /// </summary>
    public struct AttackData
    {
        /// <summary>攻击实例 ID。</summary>
        public int AttackInstanceId;
        /// <summary>发起攻击的对象实例 ID。</summary>
        public int SourceInstanceId;
        /// <summary>发射器端口索引。</summary>
        public int TransmitterPortIndex;
        /// <summary>由输入明确选中的追踪目标；空值由投射物轨迹自行选择。</summary>
        public IDamageable TrackingTarget;
        /// <summary>投射物配置。</summary>
        public ProjectileProfileSO ProjectileProfile;
        /// <summary>攻击发射起点。</summary>
        public Vector3 OriginPosition;
        /// <summary>沿最终发射方向应用的投射物生成偏移。</summary>
        public float ProjectileSpawnOffset;
        /// <summary>攻击发射方向。</summary>
        public Vector3 LaunchDirection;
        /// <summary>最终攻击伤害量。</summary>
        public float DamageAmount;
        /// <summary>单次攻击消耗的能量。</summary>
        public float EnergyCost;
        /// <summary>攻击执行时记录的实付能量成本。</summary>
        public float PaidEnergyCostSnapshot;
        /// <summary>指示实付能量成本快照已明确设置，包括零成本。</summary>
        public bool HasPaidEnergyCostSnapshot;
        /// <summary>本次攻击每秒允许的攻击次数。</summary>
        public float AttacksPerSecond;
        /// <summary>本次攻击生成的投射物数量。</summary>
        public int ProjectileCount;
        /// <summary>本次攻击的穿透次数。</summary>
        public int PenetrationCount;
        /// <summary>伤害类型。</summary>
        public DamageType DamageType;
        /// <summary>投射物速度。</summary>
        public float SpeedUnitsPerSecond;
        /// <summary>投射物最大旅行距离。</summary>
        public float MaximumTravelDistance;
        /// <summary>投射物碰撞半径。</summary>
        public float CollisionRadius;
        /// <summary>指示本次攻击是否暴击。</summary>
        public bool WasCritical;
        /// <summary>本次攻击允许命中的目标阵营。</summary>
        public ProjectileTargetMode TargetMode;
    }
}
