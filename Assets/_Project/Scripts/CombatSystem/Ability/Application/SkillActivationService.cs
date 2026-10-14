using System;
using System.Collections.Generic;
using Interfaces;
using Services;
using Unity.Profiling;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace CombatSystem
{
    /// <summary>编排玩家整卡释放：检查、准备、消费、提交、更新状态；敌人直接使用执行器。</summary>
    public sealed class SkillActivationService : ITickable
    {
        private static readonly ProfilerMarker TickMarker =
            new("3DBuYu/VContainer/SkillActivationService.Tick");

        private readonly IEnergyService _energy;
        private readonly ICombatPhaseService _phase;
        private readonly IDamageRequestProcessor _damage;
        private readonly SkillTargetResolver _targets;
        private readonly SkillHand _hand;
        private readonly ICombatItemConsumptionService _items;
        private readonly ISkillExecutionService _executor;
        private readonly Play.CentralCore _caster;
        private readonly CentralSkillLoadout _centralSkillLoadout;
        private bool _isExecuting;

        /// <summary>注入玩家规则和唯一执行入口，并从容器可选查询场景施法者。</summary>
        [Inject]
        public SkillActivationService(
            IEnergyService energyService,
            ICombatPhaseService phaseService,
            IDamageRequestProcessor damageProcessor,
            ISpatialQueryService spatialQueryService,
            SkillHand skillHand,
            ICombatItemConsumptionService itemConsumptionService,
            ISkillExecutionService skillExecutionService,
            IObjectResolver resolver)
        {
            _energy = energyService;
            _phase = phaseService;
            _damage = damageProcessor;
            _targets = new SkillTargetResolver(spatialQueryService);
            _hand = skillHand;
            _items = itemConsumptionService;
            _executor = skillExecutionService ?? throw new ArgumentNullException(nameof(skillExecutionService));
            _caster = resolver.TryResolve(out Play.CentralCore caster) ? caster : null;
            _centralSkillLoadout = resolver.TryResolve(out CentralSkillLoadout centralSkillLoadout)
                ? centralSkillLoadout
                : null;
        }

        /// <summary>查询基础规则与目标存在性，不创建施法快照、不消费资源。</summary>
        public SkillActivationFailureReason CheckAvailability(int cardId)
        {
            SkillActivationFailureReason reason = CheckConditions(cardId, out SkillCardState card);
            if (reason != SkillActivationFailureReason.None)
            {
                return reason;
            }

            Vector3 origin = CasterPosition();
            foreach (SkillDefinitionSO definition in card.SkillDefinitions)
            {
                if (!_targets.HasTargets(definition, origin, out reason))
                {
                    return reason;
                }
            }
            return SkillActivationFailureReason.None;
        }

        /// <summary>同步释放一张卡；伤害与物品事件不能重入，能量终局延迟到动作结束。</summary>
        public SkillActivationResult TryActivate(int cardId)
        {
            if (_isExecuting)
            {
                return SkillActivationResult.Failure(SkillActivationFailureReason.ActionFailed);
            }

            _isExecuting = true;
            try
            {
                using IDisposable action = (_energy as IEnergyActionScope)?.BeginAction();
                return Activate(cardId);
            }
            finally
            {
                _isExecuting = false;
            }
        }

        /// <summary>完整准备后消费来源并提交；失败退出自动取消尚未提交的投射物。</summary>
        private SkillActivationResult Activate(int cardId)
        {
            SkillActivationFailureReason reason = CheckConditions(cardId, out SkillCardState card);
            if (reason != SkillActivationFailureReason.None)
            {
                return Failure(reason, card);
            }

            if (_energy != null && !_energy.TrySpend(
                    GetItemAdjustedEnergyCost(card.EnergyCost),
                    EnergySpendKind.Skill))
            {
                return Failure(SkillActivationFailureReason.InsufficientEnergy, card);
            }

            using var batch = SkillDamageBatch.CreateTransaction(card.SourceItemInstanceId);
            reason = Prepare(card, batch);
            if (reason != SkillActivationFailureReason.None || (batch.Count > 0 && _damage == null))
            {
                _energy?.AddEnergy(GetEffectiveEnergyCost(card.EnergyCost));
                return Failure(reason != SkillActivationFailureReason.None
                    ? reason : SkillActivationFailureReason.ActionFailed, card);
            }

            bool consumesItem = card.RemainingUses == 1;
            if (consumesItem && !_items.TryConsumeItemInstance(card.SourceItemInstanceId))
            {
                _energy?.AddEnergy(GetEffectiveEnergyCost(card.EnergyCost));
                return Failure(SkillActivationFailureReason.SourceConsumptionFailed, card);
            }

            int appliedCount = batch.Commit(_damage);
            card.ConsumeUse();
            card.BeginCooldown();
            if (consumesItem)
            {
                _hand.RemoveByCardId(card.CardId);
            }
            else
            {
                _hand.NotifySkillStateChanged(card);
            }

            return new SkillActivationResult
            {
                Succeeded = true,
                FailureReason = SkillActivationFailureReason.None,
                AppliedTargetCount = appliedCount,
                ConsumedSourceItem = consumesItem,
                RemainingUses = card.RemainingUses,
                RemainingCooldownSeconds = card.RemainingCooldownSeconds
            };
        }

        /// <summary>先固定全部目标和物品强度，再调用动作收集伤害；任何动作失败都不提交批次。</summary>
        private SkillActivationFailureReason Prepare(SkillCardState card, SkillDamageBatch batch)
        {
            Vector3 origin = CasterPosition();
            var requests = new List<SkillExecutionRequest>(card.SkillDefinitions.Count);
            foreach (SkillDefinitionSO definition in card.SkillDefinitions)
            {
                if (!_targets.TryResolve(definition, origin, out IReadOnlyList<IDamageable> targets, out var reason))
                {
                    return reason;
                }
                requests.Add(new SkillExecutionRequest(
                    definition,
                    card.SourceItemInstanceId,
                    origin,
                    targets,
                    batch,
                    card.PowerLevel));
            }

            foreach (SkillExecutionRequest request in requests)
            {
                if (!_executor.Execute(in request).Succeeded)
                {
                    return SkillActivationFailureReason.ActionFailed;
                }
            }
            return SkillActivationFailureReason.None;
        }

        /// <summary>按一致优先级检查卡牌、阶段、冷却、次数、来源、配置与能量。</summary>
        private SkillActivationFailureReason CheckConditions(int cardId, out SkillCardState card)
        {
            card = _hand?.FindByCardId(cardId);
            if (card == null)
            {
                return SkillActivationFailureReason.SkillNotFound;
            }
            if (_phase != null && !_phase.CanPerformCombatActions)
            {
                return SkillActivationFailureReason.CombatActionsNotAllowed;
            }
            if (card.IsOnCooldown)
            {
                return SkillActivationFailureReason.CooldownActive;
            }
            if (!card.HasRemainingUse)
            {
                return SkillActivationFailureReason.UsesExhausted;
            }
            if (_items == null || !_items.CanConsumeItemInstance(card.SourceItemInstanceId))
            {
                return SkillActivationFailureReason.SourceItemUnavailable;
            }
            if (card.SkillDefinitions.Count == 0)
            {
                return SkillActivationFailureReason.SkillNotFound;
            }
            foreach (SkillDefinitionSO definition in card.SkillDefinitions)
            {
                if (definition == null)
                {
                    return SkillActivationFailureReason.SkillNotFound;
                }
            }
            if (_energy != null && _energy.CurrentEnergy.CurrentValue < GetEffectiveEnergyCost(card.EnergyCost))
            {
                return SkillActivationFailureReason.InsufficientEnergy;
            }
            return SkillActivationFailureReason.None;
        }

        /// <summary>将技能基础消耗换算为当前阶段的实际消耗。</summary>
        /// <param name="baseAmount">技能配置中的基础消耗。</param>
        /// <returns>应用当前能量倍率后的消耗。</returns>
        private float GetEffectiveEnergyCost(float baseAmount)
        {
            float itemAdjustedCost = GetItemAdjustedEnergyCost(baseAmount);
            return _energy is IEnergyCostService energyCostService
                ? energyCostService.GetEffectiveCost(itemAdjustedCost)
                : itemAdjustedCost;
        }

        /// <summary>应用中央背包的节能道具倍率，不重复应用本局通用消耗倍率。</summary>
        private float GetItemAdjustedEnergyCost(float baseAmount)
        {
            return baseAmount * Mathf.Max(
                0f,
                _centralSkillLoadout?.EnergyCostMultiplier ?? 1f);
        }

        /// <summary>读取本次施法中心；无场景施法者的测试使用原点。</summary>
        private Vector3 CasterPosition() => _caster != null ? _caster.transform.position : Vector3.zero;

        /// <summary>推进本局全部卡牌冷却，包括已卸装实例。</summary>
        public void AdvanceCooldown(float deltaTime) => _hand?.AdvanceCooldowns(deltaTime);

        /// <summary>使用游戏时间推进冷却，暂停时停止。</summary>
        public void Tick()
        {
            using var profilerScope = TickMarker.Auto();
            AdvanceCooldown(Time.deltaTime);
        }

        /// <summary>返回失败原因和未经本次消费修改的卡牌状态。</summary>
        private static SkillActivationResult Failure(SkillActivationFailureReason reason, SkillCardState card)
        {
            return new SkillActivationResult
            {
                FailureReason = reason,
                RemainingUses = card?.RemainingUses ?? 0,
                RemainingCooldownSeconds = card?.RemainingCooldownSeconds ?? 0f
            };
        }
    }
}
