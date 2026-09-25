using System;
using System.Collections.Generic;

namespace EnemySystem
{
    /// <summary>管理一次性特殊敌人时间规则，并保留外部触发接缝。</summary>
    public sealed class SpecialEnemySpawnSchedule
    {
        private IReadOnlyList<SpecialEnemySpawnRule> _rules = Array.Empty<SpecialEnemySpawnRule>();
        private readonly HashSet<string> _triggered = new();

        public SpecialEnemySpawnSchedule(IReadOnlyList<SpecialEnemySpawnRule> rules)
        {
            Replace(rules);
        }

        /// <summary>替换未触发规则，已触发的规则 ID 不会重复执行。</summary>
        public void Replace(IReadOnlyList<SpecialEnemySpawnRule> rules)
        {
            _rules = rules ?? Array.Empty<SpecialEnemySpawnRule>();
        }

        /// <summary>触发所有已经到达时间且尚未完成的规则。</summary>
        public void Tick(float elapsedTime, Func<SpecialEnemySpawnRule, bool> trigger)
        {
            if (trigger == null || elapsedTime < 0f || float.IsNaN(elapsedTime) || float.IsInfinity(elapsedTime))
            {
                return;
            }

            for (int index = 0; index < _rules.Count; index++)
            {
                SpecialEnemySpawnRule rule = _rules[index];
                if (rule == null || !rule.IsValid || _triggered.Contains(rule.Id) ||
                    elapsedTime < Math.Max(0f, rule.TriggerTimeSeconds))
                {
                    continue;
                }

                if (trigger(rule))
                {
                    _triggered.Add(rule.Id);
                }
            }
        }

        /// <summary>按规则 ID 提供未来事件系统使用的触发入口。</summary>
        public bool TryTrigger(string ruleId, Func<SpecialEnemySpawnRule, bool> trigger)
        {
            if (string.IsNullOrWhiteSpace(ruleId) || trigger == null || _triggered.Contains(ruleId))
            {
                return false;
            }

            for (int index = 0; index < _rules.Count; index++)
            {
                SpecialEnemySpawnRule rule = _rules[index];
                if (rule == null || !rule.IsValid || rule.Id != ruleId)
                {
                    continue;
                }

                if (!trigger(rule))
                {
                    return false;
                }

                _triggered.Add(ruleId);
                return true;
            }

            return false;
        }
    }
}
