using Interfaces;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 描述一次已经从目标生命中实际扣除的伤害。
    /// <para>该事件是受击材质、跳字、命中特效和镜头反馈的唯一事实来源。</para>
    /// </summary>
    public readonly struct DamageEvent
    {
        /// <summary>实际受伤目标。</summary>
        public IDamageable Target { get; }

        /// <summary>目标当前实例标识，用于跳字聚合键。</summary>
        public int TargetInstanceId { get; }

        /// <summary>攻击运行时标识。</summary>
        public int AttackInstanceId { get; }

        /// <summary>伤害来源运行时标识。</summary>
        public int SourceInstanceId { get; }

        /// <summary>命中世界位置。</summary>
        public Vector3 HitPosition { get; }

        /// <summary>命中表面的世界法线。</summary>
        public Vector3 HitNormal { get; }

        /// <summary>实际扣除的生命值。</summary>
        public float AppliedDamage { get; }

        /// <summary>伤害类型。</summary>
        public DamageType DamageType { get; }

        /// <summary>攻击构建阶段是否判定为暴击。</summary>
        public bool WasCritical { get; }

        /// <summary>本次伤害是否使目标死亡。</summary>
        public bool WasKilled { get; }

        /// <summary>处理后的剩余生命值。</summary>
        public float RemainingHealth { get; }

        /// <summary>从请求和结果创建实际扣血事件。</summary>
        public DamageEvent(IDamageable target, in DamageRequest request, in DamageResult result)
        {
            Target = target;
            TargetInstanceId = target?.Transform != null ? target.Transform.GetInstanceID() : 0;
            AttackInstanceId = request.AttackInstanceId;
            SourceInstanceId = request.SourceInstanceId;
            HitPosition = request.HitPosition;
            HitNormal = request.HitNormal;
            AppliedDamage = Mathf.Max(0f, result.AppliedDamage);
            DamageType = request.DamageType;
            WasCritical = request.WasCritical;
            WasKilled = result.WasKilled;
            RemainingHealth = Mathf.Max(0f, result.RemainingHealth);
        }
    }
}
