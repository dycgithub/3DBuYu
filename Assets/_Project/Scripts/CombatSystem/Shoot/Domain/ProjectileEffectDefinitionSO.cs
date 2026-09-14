using Services;
using System.Collections.Generic;
using Interfaces;
using SpatialSystem.Bridge;
using UnityEngine;
using EffectSystem;
using UnityEngine.Serialization;

namespace CombatSystem
{
    /// <summary>
    /// 定义投射物命中、命中前或过期时可执行的效果策略。
    /// </summary>
    public abstract class ProjectileEffectDefinitionSO : ScriptableObject
    {
        /// <summary>
        /// 在投射物效果上下文中执行当前效果。
        /// </summary>
        /// <param name="context">包含来源、目标、命中位置和伤害结果的上下文。</param>
        /// <param name="effectService">可选的视觉效果服务。</param>
        public abstract void ExecuteEffect(ProjectileEffectContext context, IEffectService effectService);
    }

    /// <summary>
    /// 在投射物命中且目标未死亡时施加 Buff。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Attack Effects/Apply Buff")]
    public sealed class ApplyBuffEffectDefinitionSO : ProjectileEffectDefinitionSO
    {
        /// <summary>要施加给命中目标的 Buff 定义。</summary>
        [FormerlySerializedAs("Buff")]
        public BuffDefinitionSO BuffDefinition;

        /// <summary>
        /// 将配置的 Buff 应用到命中目标的父级接收者。
        /// </summary>
        /// <param name="context">投射物效果上下文。</param>
        /// <param name="effectService">未使用的视觉效果服务。</param>
        /// <remarks>击杀、空目标或空 Buff 定义时跳过；当前不使用 effectService。</remarks>
        public override void ExecuteEffect(ProjectileEffectContext context, IEffectService effectService)
        {
            if (BuffDefinition == null || context.WasKilled || context.TargetGameObject == null)
            {
                return;
            }

            context.TargetGameObject.GetComponentInParent<IBuffReceiver>()?.ApplyBuff(
                BuffDefinition,
                context.SourceInstanceId);
        }
    }

    /// <summary>
    /// 在命中位置或目标位置播放配置的 VFX 效果。
    /// </summary>
    [CreateAssetMenu(menuName = "Combat/Attack Effects/Play VFX")]
    public sealed class PlayVFXEffectDefinitionSO : ProjectileEffectDefinitionSO
    {
        /// <summary>要播放的效果标识。</summary>
        [FormerlySerializedAs("Effect")]
        public EffectId EffectId = EffectId.None;

        /// <summary>指示效果是否挂到命中目标的 Transform 下。</summary>
        [FormerlySerializedAs("AttachToTarget")]
        public bool AttachEffectToTarget;

        /// <summary>
        /// 根据配置在命中位置播放一次视觉效果。
        /// </summary>
        /// <param name="context">投射物效果上下文。</param>
        /// <param name="effectService">负责实例化视觉效果的服务。</param>
        /// <remarks>启用附着时使用目标当前位置而不是命中点作为播放位置。</remarks>
        public override void ExecuteEffect(ProjectileEffectContext context, IEffectService effectService)
        {
            if (EffectId == EffectId.None || effectService == null)
            {
                return;
            }

            Transform parent = null;
            Vector3 position = context.HitPosition;
            if (AttachEffectToTarget && context.TargetGameObject != null)
            {
                parent = context.TargetGameObject.transform;
                position = parent.position;
            }

            effectService.Play(EffectId, position, parent);
        }
    }

    /// <summary>在主目标命中位置对范围内其他敌人提交一次伤害。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Effects/Explode On Hit")]
    public sealed class ExplodeOnHitEffectDefinitionSO : ProjectileEffectDefinitionSO
    {
        /// <summary>爆炸影响半径。</summary>
        [Min(0f)] public float Radius = 2.5f;

        /// <summary>
        /// 通过空间查询获取范围目标，并为每个唯一目标提交统一伤害请求。
        /// </summary>
        public override void ExecuteEffect(ProjectileEffectContext context, IEffectService effectService)
        {
            if (context.DamageProcessor == null || context.SpatialQueryService == null)
            {
                return;
            }

            if (context.ProjectileState != null && context.ProjectileState.HasTriggeredExplosion)
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
                SpatialRegistry.LAYER_ENEMY);
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
                request.HitPosition = target.Position;
                request.HitNormal = Vector3.up;
                context.DamageProcessor.TryApplyDamage(target, in request, out _);
            }
        }

        /// <summary>使用 GameObject 和组件实例 ID 兼容不同目标实现的主目标引用。</summary>
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

        /// <summary>同时兼容目标 GameObject ID 和具体伤害组件 ID。</summary>
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

    /// <summary>将命中投射物的冲击提交给敌人运动效果接缝。</summary>
    [CreateAssetMenu(menuName = "Combat/Attack Effects/Knockback")]
    public sealed class KnockbackEffectDefinitionSO : ProjectileEffectDefinitionSO
    {
        /// <summary>默认击退冲量。</summary>
        [Min(0f)] public float Impulse = 5f;

        /// <summary>通过 IEnemyMotionEffectTarget 提交击退，不依赖 ECS 实现。</summary>
        public override void ExecuteEffect(ProjectileEffectContext context, IEffectService effectService)
        {
            if (context.TargetGameObject == null)
            {
                return;
            }

            IEnemyMotionEffectTarget motionTarget = context.TargetGameObject
                .GetComponentInParent<IEnemyMotionEffectTarget>();
            if (motionTarget == null)
            {
                return;
            }

            Vector3 direction = context.DamageRequest.ImpactDirection;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = -context.DamageRequest.HitNormal;
            }

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            motionTarget.AddExternalImpulse(direction.normalized * Mathf.Max(0f, Impulse));
        }
    }
}
