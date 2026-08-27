using UnityEngine;

namespace CombatSystem
{
    /// <summary>由资产定义的一次攻击参数修改规则。</summary>
    public abstract class AttackModifierDefinitionSO : ScriptableObject, IAttackModifier
    {
        public abstract void Modify(ref AttackInfo info);
    }
}
