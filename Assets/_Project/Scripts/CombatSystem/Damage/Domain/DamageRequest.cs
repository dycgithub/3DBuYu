using UnityEngine;
using UnityEngine.Serialization;

namespace CombatSystem
{
    /// <summary>
    /// 描述一次待处理伤害及发起该命中的攻击返能数据。
    /// </summary>
    public struct DamageRequest
    {
        /// <summary>产生这次伤害的攻击运行时标识。</summary>
        [FormerlySerializedAs("AttackId")]
        public int AttackInstanceId;

        /// <summary>该攻击支付的基础能量成本，尚未应用全局消耗倍率。</summary>
        public float EnergyCost;

        /// <summary>攻击发射时记录的实付能量成本。</summary>
        public float PaidEnergyCostSnapshot;

        /// <summary>指示实付能量成本快照已明确设置，包括零成本。</summary>
        public bool HasPaidEnergyCostSnapshot;

        /// <summary>该攻击击杀目标时返还能量的配置比例。</summary>
        public float KillEnergyRefundRatio;

        /// <summary>产生这次伤害的来源运行时标识。</summary>
        [FormerlySerializedAs("SourceId")]
        public int SourceInstanceId;

        /// <summary>提交给目标的伤害量，单位由目标生命值系统定义。</summary>
        [FormerlySerializedAs("BaseDamage")]
        public float DamageAmount;

        /// <summary>伤害类型；当前目标是否按类型计算抗性由接收者决定。</summary>
        public DamageType DamageType;

        /// <summary>命中位置，使用世界坐标。</summary>
        [FormerlySerializedAs("HitPoint")]
        public Vector3 HitPosition;

        /// <summary>命中表面的世界空间法线。</summary>
        public Vector3 HitNormal;

        /// <summary>产生当前请求的投射物冲击方向。</summary>
        public Vector3 ImpactDirection;

        /// <summary>指示攻击构建阶段是否判定为暴击。</summary>
        [FormerlySerializedAs("IsCritical")]
        public bool WasCritical;
    }
}
