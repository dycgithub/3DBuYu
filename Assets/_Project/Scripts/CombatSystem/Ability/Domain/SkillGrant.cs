using System;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 一张物品技能卡共享的消费与冷却配置；技能列表由物品定义唯一提供。
    /// </summary>
    [Serializable]
    public sealed class SkillGrant
    {
        [Min(0f)] [SerializeField] private float energyCost;
        [Min(1)] [SerializeField] private int useCount = 1;
        [Min(0f)] [SerializeField] private float cooldownSeconds;

        public float EnergyCost => energyCost;
        public int UseCount => Mathf.Max(1, useCount);
        public float CooldownSeconds => Mathf.Max(0f, cooldownSeconds);

        /// <summary>创建整卡消费配置，约束非负消耗和至少一次使用。</summary>
        public SkillGrant(
            float energyCost,
            int useCount,
            float cooldownSeconds)
        {
            this.energyCost = Mathf.Max(0f, energyCost);
            this.useCount = Mathf.Max(1, useCount);
            this.cooldownSeconds = Mathf.Max(0f, cooldownSeconds);
        }

        private SkillGrant()
        {
        }
    }
}
