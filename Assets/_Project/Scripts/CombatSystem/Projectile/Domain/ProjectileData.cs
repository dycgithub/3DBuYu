using UnityEngine;
using Interfaces;

namespace CombatSystem
{
    /// <summary>
    /// 保存投射物生成参数、世界射程球心和飞行期间使用的数据快照。
    /// </summary>
    public struct ProjectileData
    {
        /// <summary>投射物实例 ID。</summary>
        public int ProjectileInstanceId;
        /// <summary>所属攻击实例 ID。</summary>
        public int AttackInstanceId;
        /// <summary>发起攻击的对象实例 ID。</summary>
        public int SourceInstanceId;
        /// <summary>本次攻击明确选择的追踪目标。</summary>
        public IDamageable TrackingTarget;
        /// <summary>投射物配置。</summary>
        public ProjectileProfileSO ProjectileProfile;
        /// <summary>投射物生成时复制的配置快照。</summary>
        public ProjectileProfileSO ProjectileProfileSnapshot;
        /// <summary>发射起点。</summary>
        public Vector3 OriginPosition;
        /// <summary>限制投射物活动范围的世界球心。</summary>
        public Vector3 RangeOriginPosition;
        /// <summary>指示范围球心已由攻击来源显式设置。</summary>
        public bool HasWorldRangeOrigin;
        /// <summary>尚未应用到最终发射起点的偏移。</summary>
        public float ProjectileSpawnOffset;
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
        /// <summary>一次攻击实际支付的基础能量。</summary>
        public float EnergyCost;
        /// <summary>攻击发射时记录的实付能量成本。</summary>
        public float PaidEnergyCostSnapshot;
        /// <summary>指示实付能量成本快照已明确设置，包括零成本。</summary>
        public bool HasPaidEnergyCostSnapshot;
        /// <summary>剩余穿透次数。</summary>
        public int PenetrationCount;
        /// <summary>伤害类型。</summary>
        public DamageType DamageType;
        /// <summary>指示本次攻击是否暴击。</summary>
        public bool WasCritical;
        /// <summary>本次投射物允许命中的目标阵营。</summary>
        public ProjectileTargetMode TargetMode;

        /// <summary>标记分裂生成的子弹，防止再次触发分裂。</summary>
        public bool IsSplitChild;

        /// <summary>标记速度随机变化已应用，避免分裂子弹重复随机。</summary>
        public bool HasAppliedSpeedVariance;

        /// <summary>本次攻击的每秒攻击次数，供顺序发射计算批次时长。</summary>
        public float AttacksPerSecond;

        /// <summary>标记弹速倍率已应用，避免分裂子弹重复加速。</summary>
        public bool HasAppliedSpeedMultiplier;
    }
}
