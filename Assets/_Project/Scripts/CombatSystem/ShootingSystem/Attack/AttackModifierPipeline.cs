using System.Collections.Generic;

namespace CombatSystem
{
    /// <summary>按装备顺序应用所有攻击修改器，并在末尾做基本范围校正。</summary>
    public sealed class AttackModifierPipeline
    {
        public void Apply(
            ref AttackInfo info,
            IReadOnlyList<IAttackModifier> modifiers)
        {
            if (modifiers == null)
                return;

            for (int i = 0; i < modifiers.Count; i++)
                modifiers[i]?.Modify(ref info);

            info.Damage = UnityEngine.Mathf.Max(0f, info.Damage);
            info.ProjectileCount = UnityEngine.Mathf.Max(1, info.ProjectileCount);
            info.Penetration = UnityEngine.Mathf.Max(0, info.Penetration);
        }
    }
}
