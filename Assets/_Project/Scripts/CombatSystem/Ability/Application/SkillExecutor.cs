using System;
using System.Collections.Generic;
using Interfaces;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>执行显式目标技能；玩家与敌人共用，不持有卡牌或玩家资源状态。</summary>
    public sealed class SkillExecutor : ISkillExecutionService
    {
        private readonly IDamageRequestProcessor _damageProcessor;
        private readonly IAttackInstanceIdGenerator _attackIds;
        private readonly ICentralSkillEffectService _centralEffects;

        /// <summary>注入默认伤害入口、攻击编号来源和中央场景效果接缝。</summary>
        public SkillExecutor(
            IDamageRequestProcessor damageProcessor,
            IAttackInstanceIdGenerator attackIds,
            ICentralSkillEffectService centralEffects = null)
        {
            _damageProcessor = damageProcessor;
            _attackIds = attackIds ?? throw new ArgumentNullException(nameof(attackIds));
            _centralEffects = centralEffects;
        }

        /// <summary>使用调用方提供的技能、来源和目标执行一次动作。</summary>
        public SkillExecutionResult Execute(
            SkillDefinitionSO definition, int sourceInstanceId, Vector3 sourcePosition,
            IReadOnlyList<IDamageable> targets)
        {
            var request = new SkillExecutionRequest(definition, sourceInstanceId, sourcePosition, targets);
            return Execute(in request);
        }

        /// <summary>检查请求并创建独立上下文；未指定动作资产时执行默认伤害。</summary>
        public SkillExecutionResult Execute(in SkillExecutionRequest request)
        {
            SkillDefinitionSO definition = request.Definition;
            if (definition == null)
            {
                return SkillExecutionResult.Failure(SkillExecutionFailureReason.SkillNotFound);
            }

            IReadOnlyList<IDamageable> targets = request.Targets ?? Array.Empty<IDamageable>();
            if (targets.Count == 0 && !definition.AllowNoTarget)
            {
                return SkillExecutionResult.Failure(SkillExecutionFailureReason.NoValidTarget);
            }

            IDamageRequestProcessor damage = request.DamageProcessor ?? _damageProcessor;
            var context = new SkillExecutionContext(
                in request,
                _attackIds.Next(),
                _centralEffects);
            int acceptedCount;
            bool succeeded = definition.ActionDefinition != null
                ? definition.ActionDefinition.TryExecute(in context, targets, damage, out acceptedCount)
                : DamageSkillActionDefinitionSO.Execute(in context, targets, damage, out acceptedCount);

            return succeeded
                ? new SkillExecutionResult(true, SkillExecutionFailureReason.None, acceptedCount)
                : SkillExecutionResult.Failure(SkillExecutionFailureReason.ActionFailed);
        }
    }
}
