using System.Collections.Generic;
using Interfaces;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 描述调用方已经准备好的技能和目标；执行器另行分配单次攻击身份。
    /// </summary>
    public readonly struct SkillExecutionRequest
    {
        /// <summary>要执行的技能静态定义。</summary>
        public SkillDefinitionSO Definition { get; }

        /// <summary>发起技能的实体实例标识。</summary>
        public int SourceInstanceId { get; }

        /// <summary>发起技能的实体世界坐标。</summary>
        public Vector3 SourcePosition { get; }

        /// <summary>已经由调用方选择的目标快照。</summary>
        public IReadOnlyList<IDamageable> Targets { get; }

        /// <summary>可选的伤害处理器；为空时使用执行服务的默认处理器。</summary>
        public IDamageRequestProcessor DamageProcessor { get; }

        /// <summary>创建一次显式目标技能执行请求。</summary>
        /// <param name="definition">要执行的技能定义。</param>
        /// <param name="sourceInstanceId">发起技能的实体实例标识。</param>
        /// <param name="sourcePosition">发起技能的世界坐标。</param>
        /// <param name="targets">调用方已经选择的目标；允许为空。</param>
        /// <param name="damageProcessor">可选的伤害处理器。</param>
        public SkillExecutionRequest(
            SkillDefinitionSO definition,
            int sourceInstanceId,
            Vector3 sourcePosition,
            IReadOnlyList<IDamageable> targets,
            IDamageRequestProcessor damageProcessor = null)
        {
            Definition = definition;
            SourceInstanceId = sourceInstanceId;
            SourcePosition = sourcePosition;
            Targets = targets;
            DamageProcessor = damageProcessor;
        }
    }
}
