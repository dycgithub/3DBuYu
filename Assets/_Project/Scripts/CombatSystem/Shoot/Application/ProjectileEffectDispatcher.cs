using Interfaces;
using Services;
using UnityEngine;
using EffectSystem;
using System.Collections.Generic;

namespace CombatSystem
{
    /// <summary>
    /// 将投射物生命周期事件转换为配置效果或默认视觉效果。
    /// </summary>
    public sealed class ProjectileEffectDispatcher :
        IProjectileEffectDispatcher,
        IProjectileStateEffectDispatcher
    {
        private readonly IEffectService _effectService;
        private readonly ISpatialQueryService _spatialQueryService;
        private readonly IDamageRequestProcessor _damageProcessor;
        private readonly IBuffService _buffService;
        private readonly IEnergyService _energyService;
        private readonly HashSet<int> _refundedAttackIds = new();
        private readonly Queue<int> _refundOrder = new();
        private PendingBeforeHit _pendingBeforeHit;

        private const int RefundHistoryCapacity = 512;

        /// <summary>创建投射物效果分发器。</summary>
        /// <param name="effectService">负责播放默认和自定义视觉效果的服务。</param>
        /// <param name="spatialQueryService">提供范围效果目标查询的服务。</param>
        /// <param name="damageProcessor">提交范围伤害的统一处理器。</param>
        public ProjectileEffectDispatcher(
            IEffectService effectService,
            ISpatialQueryService spatialQueryService = null,
            IDamageRequestProcessor damageProcessor = null,
            IBuffService buffService = null,
            IEnergyService energyService = null)
        {
            _effectService = effectService;
            _spatialQueryService = spatialQueryService;
            _damageProcessor = damageProcessor;
            _buffService = buffService;
            _energyService = energyService;
        }

        /// <summary>分发伤害成功后的命中效果。</summary>
        /// <param name="projectileProfile">投射物效果配置。</param>
        /// <param name="request">已提交的伤害请求。</param>
        /// <param name="result">目标返回的伤害结果。</param>
        /// <param name="target">命中的有效目标。</param>
        public void DispatchHitEffects(
            ProjectileProfileSO projectileProfile,
            DamageRequest request,
            DamageResult result,
            IDamageable target)
        {
            DispatchHitEffects(null, projectileProfile, request, result, target);
        }

        /// <summary>分发带投射物状态的命中效果。</summary>
        public void DispatchHitEffects(
            ProjectileRuntime projectile,
            ProjectileProfileSO projectileProfile,
            DamageRequest request,
            DamageResult result,
            IDamageable target)
        {
            PendingBeforeHit pendingBeforeHit = _pendingBeforeHit;
            _pendingBeforeHit = null;

            if (projectileProfile == null || !result.WasApplied)
            {
                return;
            }

            TryRefundEnergy(projectile, projectileProfile, result);

            if (pendingBeforeHit != null)
            {
                ExecuteAll(pendingBeforeHit.Effects, pendingBeforeHit.Context);
            }

            if (projectileProfile.OnHitEffectDefinitions == null ||
                projectileProfile.OnHitEffectDefinitions.Count == 0)
            {
                if (projectileProfile.HitEffectId != EffectId.None)
                {
                    _effectService?.Play(projectileProfile.HitEffectId, request.HitPosition);
                }

                return;
            }

            ExecuteAll(
                projectileProfile.OnHitEffectDefinitions,
                CreateContext(projectile, projectileProfile, request, result, target));
        }

        /// <summary>分发伤害提交前的效果。</summary>
        /// <param name="projectileProfile">投射物效果配置。</param>
        /// <param name="request">将要提交的伤害请求。</param>
        /// <param name="target">命中的有效目标。</param>
        /// <remarks>
        /// 先保存效果调用，待同一命中请求确认成功后再执行；这样拒绝的伤害不会留下 Buff 等状态。
        /// </remarks>
        public void DispatchBeforeHitEffects(
            ProjectileProfileSO projectileProfile,
            DamageRequest request,
            IDamageable target)
        {
            _pendingBeforeHit = null;
            if (projectileProfile?.BeforeHitEffectDefinitions == null)
            {
                return;
            }

            if (projectileProfile.BeforeHitEffectDefinitions.Count == 0)
            {
                return;
            }

            ProjectileEffectContext context = CreateContext(
                null,
                projectileProfile,
                request,
                default,
                target);
            _pendingBeforeHit = new PendingBeforeHit(
                projectileProfile.BeforeHitEffectDefinitions,
                context);
        }

        /// <summary>分发投射物过期效果。</summary>
        /// <param name="projectileProfile">投射物效果配置。</param>
        /// <param name="position">过期时的世界坐标。</param>
        public void DispatchExpiredEffects(ProjectileProfileSO projectileProfile, Vector3 position)
        {
            if (projectileProfile == null)
            {
                return;
            }

            if (projectileProfile.OnExpiredEffectDefinitions == null ||
                projectileProfile.OnExpiredEffectDefinitions.Count == 0)
            {
                if (projectileProfile.ExpiredEffectId != EffectId.None)
                {
                    _effectService?.Play(projectileProfile.ExpiredEffectId, position);
                }

                return;
            }

            ExecuteAll(projectileProfile.OnExpiredEffectDefinitions, new ProjectileEffectContext
            {
                HitPosition = position,
                ProjectileProfile = projectileProfile
            });
        }

        /// <summary>按配置顺序执行非空的投射物效果定义。</summary>
        /// <param name="effects">要执行的效果集合。</param>
        /// <param name="context">传给每个效果的上下文。</param>
        private void ExecuteAll(
            System.Collections.Generic.List<ProjectileEffectDefinitionSO> effects,
            ProjectileEffectContext context)
        {
            for (int i = 0; i < effects.Count; i++)
            {
                effects[i]?.ExecuteEffect(context, _effectService);
            }
        }

        /// <summary>将伤害请求和结果转换为效果执行上下文。</summary>
        /// <param name="projectileProfile">产生事件的投射物配置。</param>
        /// <param name="request">伤害请求。</param>
        /// <param name="result">伤害处理结果。</param>
        /// <param name="target">命中的目标。</param>
        /// <returns>供效果定义使用的上下文。</returns>
        private ProjectileEffectContext CreateContext(
            ProjectileRuntime projectile,
            ProjectileProfileSO projectileProfile,
            DamageRequest request,
            DamageResult result,
            IDamageable target)
        {
            GameObject targetObject = target?.Transform != null ? target.Transform.gameObject : null;
            return new ProjectileEffectContext
            {
                SourceInstanceId = request.SourceInstanceId,
                TargetInstanceId = targetObject != null ? targetObject.GetInstanceID() : 0,
                TargetGameObject = targetObject,
                WasKilled = result.WasKilled,
                HitPosition = request.HitPosition,
                AppliedDamage = result.AppliedDamage,
                ProjectileProfile = projectileProfile,
                DamageRequest = request,
                DamageProcessor = _damageProcessor,
                SpatialQueryService = _spatialQueryService,
                ProjectileState = projectile,
                BuffService = _buffService
            };
        }

        /// <summary>按每次攻击最多一次的规则返还击杀能量。</summary>
        private void TryRefundEnergy(
            ProjectileRuntime projectile,
            ProjectileProfileSO profile,
            DamageResult result)
        {
            if (projectile == null || _energyService == null || !result.WasKilled ||
                projectile.Data.AttackInstanceId <= 0 || profile.KillEnergyRefundRatio <= 0f ||
                !_refundedAttackIds.Add(projectile.Data.AttackInstanceId))
            {
                return;
            }

            float baseCost = Mathf.Max(0f, projectile.Data.EnergyCost);
            float paidCost = _energyService is IEnergyCostService costService
                ? costService.GetEffectiveCost(baseCost)
                : baseCost;
            float refund = paidCost * Mathf.Clamp01(profile.KillEnergyRefundRatio);
            if (refund > 0f)
            {
                _energyService.AddEnergy(refund);
            }

            _refundOrder.Enqueue(projectile.Data.AttackInstanceId);
            while (_refundOrder.Count > RefundHistoryCapacity)
            {
                _refundedAttackIds.Remove(_refundOrder.Dequeue());
            }
        }

        private sealed class PendingBeforeHit
        {
            public readonly System.Collections.Generic.List<ProjectileEffectDefinitionSO> Effects;
            public readonly ProjectileEffectContext Context;

            public PendingBeforeHit(
                System.Collections.Generic.List<ProjectileEffectDefinitionSO> effects,
                ProjectileEffectContext context)
            {
                Effects = effects;
                Context = context;
            }
        }
    }
}
