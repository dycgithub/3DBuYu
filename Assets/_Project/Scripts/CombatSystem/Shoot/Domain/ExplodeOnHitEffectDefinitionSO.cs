using System.Collections.Generic;
using EffectSystem;
using Interfaces;
using Services;
using SpatialIndexing;
using UnityEngine;

namespace CombatSystem
{
    /// <summary>主目标命中后对邻近其他敌人造成一次不递归的范围伤害。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Effects/Explode On Hit")]
    public sealed class ExplodeOnHitEffectDefinitionSO : ProjectileEffectDefinitionSO
    {
        /// <summary>爆炸影响半径。</summary>
        [Min(0f)] public float Radius = 2.5f;
        /// <summary>副目标承受主投射物伤害的比例。</summary>
        [Range(0f, 1f)] public float DamageMultiplier = 0.5f;

        /// <summary>对范围内唯一副目标各提交一次伤害，不分发新的投射物效果。</summary>
        public override void ExecuteEffect(ProjectileEffectContext context, IEffectService effectService)
        {
            if (context.DamageProcessor == null || context.SpatialQueryService == null ||
                (context.ProjectileState != null && context.ProjectileState.HasTriggeredExplosion))
            {
                return;
            }

            if (context.ProjectileState != null)
            {
                context.ProjectileState.HasTriggeredExplosion = true;
            }

            List<IDamageable> targets = context.SpatialQueryService.QueryRadius(
                context.DamageRequest.HitPosition,
                Mathf.Max(0f, Radius),
                SpatialLayers.Enemy);
            HashSet<int> uniqueTargetIds = new();
            for (int index = 0; index < targets.Count; index++)
            {
                IDamageable target = targets[index];
                if (!TryGetTargetId(target, out int targetId) ||
                    IsMainTarget(target, targetId, context.TargetInstanceId) ||
                    !uniqueTargetIds.Add(targetId))
                {
                    continue;
                }

                DamageRequest request = context.DamageRequest;
                request.DamageAmount *= Mathf.Clamp01(DamageMultiplier);
                request.HitPosition = target.Position;
                request.HitNormal = Vector3.up;
                context.DamageProcessor.TryApplyDamage(target, in request, out _);
            }
        }

        /// <summary>获取有效目标的变换标识，用于剔除同一目标的重复碰撞体。</summary>
        private static bool TryGetTargetId(IDamageable target, out int targetId)
        {
            targetId = 0;
            if (target == null || target.Transform == null)
            {
                return false;
            }

            targetId = target.Transform.GetInstanceID();
            return targetId != 0;
        }

        /// <summary>兼容主目标对象和其 Transform 使用不同实例 ID 的目标实现。</summary>
        private static bool IsMainTarget(IDamageable target, int targetId, int mainTargetId)
        {
            if (targetId == mainTargetId)
            {
                return true;
            }

            Object targetObject = target as Object;
            return targetObject != null && targetObject.GetInstanceID() == mainTargetId;
        }
    }
}
