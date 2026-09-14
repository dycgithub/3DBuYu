using System;
using System.Collections.Generic;
using Interfaces;
using Services;
using SpatialSystem.Bridge;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>按显式施法位置自动选择目标；提示只检查存在性，施法返回独立快照。</summary>
    public sealed class SkillTargetResolver
    {
        private readonly ISpatialQueryService _spatial;

        /// <summary>注入空间查询边界，不保存场景对象或 UI 选择状态。</summary>
        public SkillTargetResolver(ISpatialQueryService spatialQueryService) => _spatial = spatialQueryService;

        /// <summary>检查目标存在性，找到一个有效目标后立即返回，不构造结果集合。</summary>
        public bool HasTargets(SkillDefinitionSO definition, Vector3 origin, out SkillActivationFailureReason reason)
        {
            reason = Validate(definition);
            if (reason != SkillActivationFailureReason.None)
            {
                return false;
            }
            if (AllowsEmpty(definition))
            {
                return true;
            }

            List<IDamageable> candidates = _spatial?.QueryAll(SpatialRegistry.LAYER_ENEMY);
            if (candidates != null)
            {
                foreach (IDamageable target in candidates)
                {
                    if (Matches(target, definition, origin))
                    {
                        return true;
                    }
                }
            }
            reason = MissingTarget(definition);
            return false;
        }

        /// <summary>复制符合规则的目标，保证后续空间查询不能覆盖本次施法快照。</summary>
        public bool TryResolve(
            SkillDefinitionSO definition, Vector3 origin,
            out IReadOnlyList<IDamageable> targets, out SkillActivationFailureReason reason)
        {
            targets = Array.Empty<IDamageable>();
            reason = Validate(definition);
            if (reason != SkillActivationFailureReason.None)
            {
                return false;
            }

            bool nearestOnly = definition.TargetSelectionMode == SkillTargetSelectionMode.NearestEnemy;
            List<IDamageable> candidates = _spatial?.QueryAll(SpatialRegistry.LAYER_ENEMY);
            var result = new List<IDamageable>();
            HashSet<IDamageable> unique = nearestOnly ? null : new HashSet<IDamageable>();
            IDamageable nearest = null;
            float nearestDistance = float.PositiveInfinity;
            if (candidates != null)
            {
                foreach (IDamageable target in candidates)
                {
                    if (!Matches(target, definition, origin))
                    {
                        continue;
                    }
                    if (nearestOnly)
                    {
                        float distance = (target.Position - origin).sqrMagnitude;
                        if (distance < nearestDistance)
                        {
                            nearest = target;
                            nearestDistance = distance;
                        }
                    }
                    else if (unique.Add(target))
                    {
                        result.Add(target);
                    }
                }
            }
            if (nearest != null)
            {
                result.Add(nearest);
            }
            if (result.Count == 0 && !AllowsEmpty(definition))
            {
                reason = MissingTarget(definition);
                return false;
            }
            targets = result;
            return true;
        }

        /// <summary>只接受已经实现的目标模式；序列化的未知数值明确拒绝。</summary>
        private static SkillActivationFailureReason Validate(SkillDefinitionSO definition)
        {
            if (definition == null)
            {
                return SkillActivationFailureReason.SkillNotFound;
            }
            return definition.TargetSelectionMode switch
            {
                SkillTargetSelectionMode.AllLivingEnemies => SkillActivationFailureReason.None,
                SkillTargetSelectionMode.NearestEnemy => SkillActivationFailureReason.None,
                SkillTargetSelectionMode.AroundCaster => SkillActivationFailureReason.None,
                _ => SkillActivationFailureReason.InvalidTarget
            };
        }

        /// <summary>共用存活与范围规则，保持可用性检查和正式解析一致。</summary>
        private static bool Matches(IDamageable target, SkillDefinitionSO definition, Vector3 origin)
        {
            return target.IsAliveAndValid() &&
                   (definition.TargetSelectionMode != SkillTargetSelectionMode.AroundCaster ||
                    (target.Position - origin).sqrMagnitude <= definition.TargetRadius * definition.TargetRadius);
        }

        /// <summary>全体和范围技能可显式允许空目标；最近单体技能始终需要一个目标。</summary>
        private static bool AllowsEmpty(SkillDefinitionSO definition) =>
            definition.AllowNoTarget && definition.TargetSelectionMode != SkillTargetSelectionMode.NearestEnemy;

        /// <summary>保留单体未找到目标与范围无目标的失败含义。</summary>
        private static SkillActivationFailureReason MissingTarget(SkillDefinitionSO definition) =>
            definition.TargetSelectionMode == SkillTargetSelectionMode.NearestEnemy
                ? SkillActivationFailureReason.InvalidTarget
                : SkillActivationFailureReason.NoValidTarget;
    }
}
