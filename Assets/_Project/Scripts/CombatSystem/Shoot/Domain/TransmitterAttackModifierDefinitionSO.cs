using UnityEngine;

namespace CombatSystem
{
    /// <summary>定义可配置地修改发射器攻击参数的策略。</summary>
    public abstract class TransmitterAttackModifierDefinitionSO : ScriptableObject
    {
        /// <summary>将当前静态修正写入本次攻击的可变构建值。</summary>
        public abstract void Apply(ref TransmitterAttackModifiers modifiers);
    }
}
