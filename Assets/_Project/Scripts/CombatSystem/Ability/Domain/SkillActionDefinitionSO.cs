using System.Collections.Generic;
using Interfaces;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 技能动作只接收本次施法上下文与目标快照；通过传入的伤害入口提交请求。
    /// 玩家准备阶段的伤害入口只接收请求，实际扣血结果在整卡提交后产生。
    /// </summary>
    public abstract class SkillActionDefinitionSO : ScriptableObject
    {
        /// <summary>处理显式目标并报告入口接收的目标数量；返回假表示动作准备失败。</summary>
        public abstract bool TryExecute(
            in SkillExecutionContext context,
            IReadOnlyList<IDamageable> targets,
            IDamageRequestProcessor damageProcessor,
            out int appliedTargetCount);
    }
}
