using System;
using System.Collections.Generic;
using Interfaces;
using Services;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>执行显式目标技能；玩家与敌人共用，不持有卡牌或玩家资源状态。</summary>
    public sealed class SkillExecutor : ISkillExecutionService
    {
        private readonly IDamageRequestProcessor _damageProcessor;
        private readonly IAttackInstanceIdGenerator _attackIds;
        private readonly ICentralSkillEffectService _centralEffects;
        private readonly IEffectService _effectService;
        private readonly IProjectileLauncher _projectiles;

        /// <summary>注入伤害、身份、中央效果、视效和可选的投射物提交边界。</summary>
        public SkillExecutor(
            IDamageRequestProcessor damageProcessor,
            IAttackInstanceIdGenerator attackIds,
            ICentralSkillEffectService centralEffects = null,
            IEffectService effectService = null,
            IProjectileLauncher projectileLauncher = null)
        {
            _damageProcessor = damageProcessor;
            _attackIds = attackIds ?? throw new ArgumentNullException(nameof(attackIds));
            _centralEffects = centralEffects;
            _effectService = effectService;
            _projectiles = projectileLauncher;
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
                _centralEffects,
                _projectiles);
            int acceptedCount;
            bool succeeded = definition.ActionDefinition != null
                ? definition.ActionDefinition.TryExecute(in context, targets, damage, out acceptedCount)
                : DamageSkillActionDefinitionSO.Execute(in context, targets, damage, out acceptedCount);

            if (succeeded)
            {
                ScheduleVisualEffect(in request, targets);
            }

            return succeeded
                ? new SkillExecutionResult(true, SkillExecutionFailureReason.None, acceptedCount)
                : SkillExecutionResult.Failure(SkillExecutionFailureReason.ActionFailed);
        }

        /// <summary>正式整卡提交延后视效；直接动作测试在成功后立即播放。</summary>
        private void ScheduleVisualEffect(
            in SkillExecutionRequest request,
            IReadOnlyList<IDamageable> targets)
        {
            SkillDefinitionSO definition = request.Definition;
            if (_effectService == null || definition.VisualEffectId == EffectSystem.EffectId.None)
            {
                return;
            }

            Vector3 sourcePosition = request.SourcePosition;
            EffectSystem.EffectId effectId = definition.VisualEffectId;
            SkillVisualEffectAnchor anchor = definition.VisualEffectAnchor;
            Action play = () => PlayVisualEffect(effectId, anchor, sourcePosition, targets);
            if (request.DamageProcessor is SkillDamageBatch batch)
            {
                batch.DeferVisualEffect(play);
                return;
            }

            play();
        }

        /// <summary>按配置的施法锚点播放瞬时技能特效。</summary>
        private void PlayVisualEffect(
            EffectSystem.EffectId effectId,
            SkillVisualEffectAnchor anchor,
            Vector3 sourcePosition,
            IReadOnlyList<IDamageable> targets)
        {
            switch (anchor)
            {
                case SkillVisualEffectAnchor.TargetsCenter:
                    PlayAtTargetCenter(effectId, sourcePosition, targets);
                    break;
                case SkillVisualEffectAnchor.EachTarget:
                    PlayAtEachTarget(effectId, targets);
                    break;
                default:
                    _effectService.Play(effectId, sourcePosition);
                    break;
            }
        }

        /// <summary>在有效目标的几何中心播放特效；没有有效目标时回退到施法者。</summary>
        private void PlayAtTargetCenter(
            EffectSystem.EffectId effectId,
            Vector3 sourcePosition,
            IReadOnlyList<IDamageable> targets)
        {
            Vector3 center = Vector3.zero;
            int targetCount = 0;
            for (int index = 0; index < targets.Count; index++)
            {
                IDamageable target = targets[index];
                if (target == null)
                {
                    continue;
                }

                center += target.Position;
                targetCount++;
            }

            _effectService.Play(
                effectId,
                targetCount > 0 ? center / targetCount : sourcePosition);
        }

        /// <summary>在每个有效目标上播放并附着一次特效。</summary>
        private void PlayAtEachTarget(
            EffectSystem.EffectId effectId,
            IReadOnlyList<IDamageable> targets)
        {
            for (int index = 0; index < targets.Count; index++)
            {
                IDamageable target = targets[index];
                if (target == null)
                {
                    continue;
                }

                _effectService.Play(effectId, target.Position, target.Transform);
            }
        }
    }
}
