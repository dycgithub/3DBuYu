using UnityEngine;

namespace CombatSystem
{
    /// <summary>一次施法的只读身份、物品强度和效果倍率，不携带卡牌、库存、次数或冷却状态。</summary>
    public readonly struct SkillExecutionContext
    {
        public SkillDefinitionSO Definition { get; }
        public int SourceInstanceId { get; }
        public Vector3 SourcePosition { get; }
        public int AttackInstanceId { get; }
        /// <summary>来源物品实例的强度等级；非物品技能为零。</summary>
        public int PowerLevel { get; }
        /// <summary>按统一物品强度表解析出的连续效果倍率。</summary>
        public float EffectScale => Supply.ItemPowerTable.GetScale(PowerLevel);
        /// <summary>由 DI 提供的中央场景效果接缝；动作资产不直接访问场景。</summary>
        public ICentralSkillEffectService CentralEffects { get; }

        /// <summary>将调用方请求与执行器分配的攻击编号组合为独立上下文。</summary>
        internal SkillExecutionContext(
            in SkillExecutionRequest request,
            int attackInstanceId,
            ICentralSkillEffectService centralEffects)
        {
            Definition = request.Definition;
            SourceInstanceId = request.SourceInstanceId;
            SourcePosition = request.SourcePosition;
            AttackInstanceId = attackInstanceId;
            PowerLevel = request.PowerLevel;
            CentralEffects = centralEffects;
        }
    }
}
