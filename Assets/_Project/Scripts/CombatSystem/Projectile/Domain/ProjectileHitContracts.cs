using System;
using Interfaces;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>投射物允许命中的目标阵营。</summary>
    public enum ProjectileTargetMode
    {
        Enemy,
        Player
    }

    /// <summary>记录一次投射物通过统一伤害管线后的命中结果。</summary>
    public readonly struct ProjectileHitContext
    {
        public ProjectileData ProjectileData { get; }
        public IDamageable Target { get; }
        public Vector3 HitPosition { get; }
        public Vector3 HitNormal { get; }
        public DamageResult DamageResult { get; }

        public ProjectileHitContext(
            ProjectileData projectileData,
            IDamageable target,
            Vector3 hitPosition,
            Vector3 hitNormal,
            DamageResult damageResult)
        {
            ProjectileData = projectileData;
            Target = target;
            HitPosition = hitPosition;
            HitNormal = hitNormal;
            DamageResult = damageResult;
        }
    }

    /// <summary>投射物命中事件的只读发布契约。</summary>
    public interface IProjectileHitEventSource
    {
        event Action<ProjectileHitContext> HitProcessed;
    }
}
