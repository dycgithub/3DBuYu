using UnityEngine;

namespace CombatSystem
{
    /// <summary>
    /// 保存中央主动技能提交的发射器全局效果，供发射器攻击系统读取。
    /// 不修改发射器配置或攻击构建规则；效果持续到来源或装备状态被清除。
    /// </summary>
    public sealed class TransmitterGlobalEffectService :
        ITransmitterGlobalEffectService,
        ITransmitterGlobalEffectResetService
    {
        /// <summary>当前全局发射器射速倍率。</summary>
        public float AttackSpeedMultiplier { get; private set; } = 1f;

        /// <summary>当前效果来源物品实例；没有效果时为零。</summary>
        public int SourceInstanceId { get; private set; }

        /// <summary>保存指定来源的全局射速倍率。</summary>
        public void SetAttackSpeedMultiplier(float multiplier, int sourceInstanceId)
        {
            AttackSpeedMultiplier = Mathf.Max(0f, multiplier);
            SourceInstanceId = sourceInstanceId;
        }

        /// <summary>只清除仍由指定来源持有的效果。</summary>
        public void ClearSource(int sourceInstanceId)
        {
            if (SourceInstanceId != sourceInstanceId)
            {
                return;
            }

            ClearAll();
        }

        /// <summary>清除当前全局装备效果并恢复默认倍率。</summary>
        public void ClearAll()
        {
            AttackSpeedMultiplier = 1f;
            SourceInstanceId = 0;
        }
    }
}
