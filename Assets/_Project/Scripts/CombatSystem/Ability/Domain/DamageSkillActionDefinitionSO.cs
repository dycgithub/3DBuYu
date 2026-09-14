using System.Collections.Generic;
using Interfaces;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>使用单次施法上下文，按技能伤害定义向显式目标提交请求。</summary>
    [CreateAssetMenu(fileName = "DamageSkillAction", menuName = "Combat/Ability Actions/Damage")]
    public sealed class DamageSkillActionDefinitionSO : SkillActionDefinitionSO
    {
        /// <summary>按配置执行伤害动作，返回伤害入口接受的目标数量。</summary>
        public override bool TryExecute(
            in SkillExecutionContext context,
            IReadOnlyList<IDamageable> targets,
            IDamageRequestProcessor damageProcessor,
            out int appliedTargetCount)
        {
            return Execute(in context, targets, damageProcessor, out appliedTargetCount);
        }

        /// <summary>执行默认伤害动作，供没有自定义动作资产的技能使用。</summary>
        public static bool Execute(
            in SkillExecutionContext context,
            IReadOnlyList<IDamageable> targets,
            IDamageRequestProcessor damageProcessor,
            out int appliedTargetCount)
        {
            return Execute(
                in context,
                targets,
                damageProcessor,
                context.Definition != null ? context.Definition.DamageAmount : 0f,
                out appliedTargetCount);
        }

        /// <summary>按指定伤害量执行默认伤害动作，供固定伤害的中央动作使用。</summary>
        internal static bool Execute(
            in SkillExecutionContext context,
            IReadOnlyList<IDamageable> targets,
            IDamageRequestProcessor damageProcessor,
            float damageAmount,
            out int appliedTargetCount)
        {
            appliedTargetCount = 0;
            if (context.Definition == null || damageProcessor == null || targets == null)
            {
                return false;
            }

            for (int index = 0; index < targets.Count; index++)
            {
                IDamageable target = targets[index];
                if (!target.IsAliveAndValid())
                {
                    continue;
                }

                DamageRequest request = new()
                {
                    AttackInstanceId = context.AttackInstanceId,
                    SourceInstanceId = context.SourceInstanceId,
                    DamageAmount = Mathf.Max(0f, damageAmount),
                    DamageType = context.Definition.DamageType,
                    HitPosition = target.Position,
                    HitNormal = target.Position.sqrMagnitude > 0f
                        ? target.Position.normalized
                        : Vector3.up
                };

                if (damageProcessor.TryApplyDamage(target, request, out DamageResult result) &&
                    result.WasApplied)
                {
                    appliedTargetCount++;
                }
            }

            return true;
        }
    }
}
