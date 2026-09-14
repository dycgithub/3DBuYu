using System.Collections.Generic;
using Interfaces;

namespace CombatSystem
{
    /// <summary>暂存整卡伤害；提交前只报告请求接收，提交后才统计实际扣血结果。</summary>
    internal sealed class SkillDamageBatch : IDamageRequestProcessor
    {
        private readonly int _sourceId;
        private readonly List<(IDamageable Target, DamageRequest Request)> _requests = new();
        internal int Count => _requests.Count;

        /// <summary>绑定整张卡唯一的来源物品身份。</summary>
        internal SkillDamageBatch(int sourceId) => _sourceId = sourceId;

        /// <summary>接收请求而不修改目标；返回结果只供准备阶段统计接收数量。</summary>
        public bool TryApplyDamage(IDamageable target, in DamageRequest request, out DamageResult result)
        {
            if (target == null)
            {
                result = new DamageResult { Outcome = DamageOutcome.Invalid };
                return false;
            }

            DamageRequest normalized = request;
            normalized.SourceInstanceId = _sourceId;
            _requests.Add((target, normalized));
            result = new DamageResult { Outcome = DamageOutcome.Applied };
            return true;
        }

        /// <summary>依配置顺序提交伤害；单个目标拒绝伤害不撤销已经生效的请求。</summary>
        internal int Commit(IDamageRequestProcessor processor)
        {
            if (processor == null)
            {
                return 0;
            }

            int appliedCount = 0;
            foreach (var pending in _requests)
            {
                if (processor.TryApplyDamage(pending.Target, pending.Request, out DamageResult result) && result.WasApplied)
                {
                    appliedCount++;
                }
            }
            return appliedCount;
        }
    }
}
