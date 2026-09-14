using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>保存一个物品实例的技能卡状态；施法临时数据由执行上下文持有。</summary>
    public sealed class SkillCardState
    {
        private static int _nextCardId;
        private readonly SkillGrant _grant;

        public int CardId { get; }
        public int SourceItemInstanceId { get; }
        public ItemDefinition SourceItemDefinition { get; }
        public IReadOnlyList<SkillDefinitionSO> SkillDefinitions { get; }
        public int RemainingUses { get; private set; }
        public float RemainingCooldownSeconds { get; private set; }
        public float EnergyCost => _grant?.EnergyCost ?? 0f;
        public float CooldownSeconds => _grant?.CooldownSeconds ?? 0f;
        public bool IsOnCooldown => RemainingCooldownSeconds > 0f;
        public bool HasRemainingUse => RemainingUses > 0;

        /// <summary>从来源身份和有序技能列表创建卡牌；没有有效技能时返回空。</summary>
        public static SkillCardState Create(
            int sourceItemInstanceId,
            ItemDefinition sourceItemDefinition,
            SkillGrant grant,
            IReadOnlyList<SkillDefinitionSO> definitions)
        {
            var skills = new List<SkillDefinitionSO>();
            if (definitions != null)
            {
                foreach (SkillDefinitionSO definition in definitions)
                {
                    if (definition != null)
                    {
                        skills.Add(definition);
                    }
                }
            }

            return skills.Count == 0
                ? null
                : new SkillCardState(sourceItemInstanceId, sourceItemDefinition, grant, skills);
        }

        /// <summary>保存只读配置引用并初始化本物品实例的使用次数。</summary>
        private SkillCardState(int sourceId, ItemDefinition item, SkillGrant grant, List<SkillDefinitionSO> skills)
        {
            CardId = Interlocked.Increment(ref _nextCardId);
            SourceItemInstanceId = sourceId;
            SourceItemDefinition = item;
            _grant = grant;
            SkillDefinitions = skills.AsReadOnly();
            RemainingUses = grant?.UseCount ?? 1;
        }

        /// <summary>判断技能顺序与授予配置是否一致，以便卸装重装时保留状态。</summary>
        internal bool Matches(IReadOnlyList<SkillDefinitionSO> definitions, SkillGrant grant)
        {
            if (_grant != grant || definitions == null)
            {
                return false;
            }

            int matched = 0;
            foreach (SkillDefinitionSO definition in definitions)
            {
                if (definition == null)
                {
                    continue;
                }
                if (matched >= SkillDefinitions.Count || SkillDefinitions[matched] != definition)
                {
                    return false;
                }
                matched++;
            }
            return matched == SkillDefinitions.Count;
        }

        /// <summary>消耗一次使用次数；耗尽时拒绝继续消费。</summary>
        public bool ConsumeUse()
        {
            if (!HasRemainingUse)
            {
                return false;
            }
            RemainingUses--;
            return true;
        }

        /// <summary>开始本卡共享的冷却。</summary>
        public void BeginCooldown()
        {
            RemainingCooldownSeconds = CooldownSeconds;
        }

        /// <summary>推进冷却，忽略负时间。</summary>
        public void AdvanceCooldown(float deltaTime)
        {
            RemainingCooldownSeconds = Mathf.Max(0f, RemainingCooldownSeconds - Mathf.Max(0f, deltaTime));
        }
    }
}
