using UnityEngine;

namespace CombatSystem
{
    /// <summary>技能动作：按配置发射一组子弹。</summary>
    [CreateAssetMenu(menuName = "ShootingSystem/Skills/Fire Projectile")]
    public sealed class FireProjectileSkillAction : SkillActionDefinition
    {
        [SerializeField] private BulletProfile profile;
        [Min(1)] [SerializeField] private int count = 1;
        [Min(0f)] [SerializeField] private float damageMultiplier = 1f;

        public override bool Execute(in SkillInfo info, SkillExecutionContext context)
        {
            if (profile == null || context?.ProjectileSpawner == null)
                return false;

            ProjectileInfo projectile = ProjectileInfoFactory.Create(
                info.Definition != null ? info.Definition.GetInstanceID() : 0,
                info.SourceId,
                profile,
                info.Origin,
                info.Direction,
                Mathf.Max(0f, profile.Damage * damageMultiplier));

            return context.ProjectileSpawner.TrySpawnBatch(in projectile, Mathf.Max(1, count));
        }
    }
}
