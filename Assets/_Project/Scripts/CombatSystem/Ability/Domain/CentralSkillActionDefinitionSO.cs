using System.Collections.Generic;
using Interfaces;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 中央背包主动技能的静态动作配置。
    /// 配置资产只描述动作类型和数值，实际场景效果由执行上下文提供的服务完成。
    /// </summary>
    [CreateAssetMenu(fileName = "CentralSkillAction", menuName = "Combat/Ability Actions/Central")]
    public sealed class CentralSkillActionDefinitionSO : SkillActionDefinitionSO
    {
        private const float DefaultPushImpulseStrength = 1f;
        private const float DefaultDamageAmount = 100f;
        private const float DefaultSlowMultiplier = 0.5f;
        private const float DefaultFireRateMultiplier = 2f;

        [SerializeField] private CentralSkillActionKind actionKind;
        [Min(0f)] [SerializeField] private float value;

        /// <summary>获取动作类型。</summary>
        public CentralSkillActionKind ActionKind => actionKind;

        /// <summary>获取动作使用的配置数值；未配置时使用动作类型的安全默认值。</summary>
        public float Value => value > 0f ? value : DefaultValue(actionKind);

        /// <summary>创建未保存到工程的动作配置，并应用五类动作的最小默认值。</summary>
        public static CentralSkillActionDefinitionSO CreateRuntime(CentralSkillActionKind kind)
        {
            CentralSkillActionDefinitionSO action = CreateInstance<CentralSkillActionDefinitionSO>();
            action.name = kind.ToString();
            action.actionKind = kind;
            action.value = DefaultValue(kind);
            return action;
        }

        /// <summary>执行中央动作并返回是否成功提交。</summary>
        public override bool TryExecute(
            in SkillExecutionContext context,
            IReadOnlyList<IDamageable> targets,
            IDamageRequestProcessor damageProcessor,
            out int appliedTargetCount)
        {
            appliedTargetCount = 0;
            switch (actionKind)
            {
                case CentralSkillActionKind.UnlockNextTransmitter:
                    return context.CentralEffects != null &&
                        context.CentralEffects.TryUnlockNextTransmitter();
                case CentralSkillActionKind.PushEnemies:
                    return context.CentralEffects != null &&
                        context.CentralEffects.TryPushEnemies(
                            targets,
                            context.SourcePosition,
                            Value);
                case CentralSkillActionKind.DamageAllEnemies:
                    return DamageSkillActionDefinitionSO.Execute(
                        in context,
                        targets,
                        damageProcessor,
                        Value,
                        out appliedTargetCount);
                case CentralSkillActionKind.SlowAllEnemies:
                    return context.CentralEffects != null &&
                        context.CentralEffects.TrySlowEnemies(
                            targets,
                            Value,
                            context.SourceInstanceId);
                case CentralSkillActionKind.DoubleTransmitterFireRate:
                    return context.CentralEffects != null &&
                        context.CentralEffects.TrySetTransmitterAttackSpeedMultiplier(
                            Value,
                            context.SourceInstanceId);
                default:
                    return false;
            }
        }

        /// <summary>返回指定中央动作的安全默认值。</summary>
        private static float DefaultValue(CentralSkillActionKind kind)
        {
            switch (kind)
            {
                case CentralSkillActionKind.PushEnemies:
                    return DefaultPushImpulseStrength;
                case CentralSkillActionKind.DamageAllEnemies:
                    return DefaultDamageAmount;
                case CentralSkillActionKind.SlowAllEnemies:
                    return DefaultSlowMultiplier;
                case CentralSkillActionKind.DoubleTransmitterFireRate:
                    return DefaultFireRateMultiplier;
                default:
                    return 0f;
            }
        }
    }
}
