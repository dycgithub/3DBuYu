using Interfaces;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>分发投射物命中、命中前和过期效果的边界接口。</summary>
    public interface IProjectileEffectDispatcher
    {
        /// <summary>分发伤害成功后的命中效果。</summary>
        /// <param name="projectileProfile">投射物效果配置。</param>
        /// <param name="request">已提交的伤害请求。</param>
        /// <param name="result">目标返回的伤害结果。</param>
        /// <param name="target">命中的有效目标。</param>
        void DispatchHitEffects(ProjectileProfileSO projectileProfile, DamageRequest request, DamageResult result, IDamageable target);
        /// <summary>分发伤害提交前的效果。</summary>
        /// <param name="projectileProfile">投射物效果配置。</param>
        /// <param name="request">将要提交的伤害请求。</param>
        /// <param name="target">命中的有效目标。</param>
        void DispatchBeforeHitEffects(ProjectileProfileSO projectileProfile, DamageRequest request, IDamageable target);
        /// <summary>分发投射物过期效果。</summary>
        /// <param name="projectileProfile">投射物效果配置。</param>
        /// <param name="position">过期时的世界坐标。</param>
        void DispatchExpiredEffects(ProjectileProfileSO projectileProfile, Vector3 position);
    }

    /// <summary>为需要读取投射物可变状态的效果提供附加分发接缝。</summary>
    public interface IProjectileStateEffectDispatcher
    {
        /// <summary>分发带投射物状态的命中效果。</summary>
        void DispatchHitEffects(
            ProjectileState projectile,
            ProjectileProfileSO projectileProfile,
            DamageRequest request,
            DamageResult result,
            IDamageable target);
    }
}
