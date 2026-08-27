using UnityEngine;

namespace CombatSystem
{
    /// <summary>增加本次攻击生成的子弹数量。</summary>
    [CreateAssetMenu(menuName = "ShootingSystem/Attack Modifiers/Add Projectile Count")]
    public sealed class AddProjectileCountAttackModifierSO : AttackModifierDefinitionSO
    {
        public int amount = 1;

        public override void Modify(ref AttackInfo info)
        {
            info.ProjectileCount = Mathf.Max(1, info.ProjectileCount + amount);
        }
    }
}
