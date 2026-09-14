using System;
using System.Collections.Generic;
using Interfaces;
using Services;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace CombatSystem
{
    /// <summary>编排玩家整卡释放：检查、准备、消费、提交、更新状态；敌人直接使用执行器。</summary>
    public sealed class SkillActivationService : ITickable
    {
        private readonly IEnergyService _energy;
        private readonly ICombatPhaseService _phase;
        private readonly IDamageRequestProcessor _damage;
        private readonly SkillTargetResolver _targets;
        private readonly SkillHand _hand;
        private readonly ICombatItemConsumptionService _items;
        private readonly ISkillExecutionService _executor;
        private readonly Play.CentralCore _caster;
        private bool _isExecuting;

        /// <summary>注入玩家规则和唯一技能执行入口；测试可省略场景施法者。</summary>
        [Inject]
        public SkillActivationService(
            IEnergyService energyService,
            ICombatPhaseService phaseService,
            IDamageRequestProcessor damageProcessor,
            ISpatialQueryService spatialQueryService,
            SkillHand skillHand,
            ICombatItemConsumptionService itemConsumptionService,
            ISkillExecutionService skillExecutionService,
            Play.CentralCore caster = null)
        {
            _energy = energyService;
            _phase = phaseService;
            _damage = damageProcessor;
            _targets = new SkillTargetResolver(spatialQueryService);
            _hand = skillHand;
            _items = itemConsumptionService;
            _executor = skillExecutionService ?? throw new ArgumentNullException(nameof(skillExecutionService));
            _caster = caster;
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

        /// <summary>准备成功后消费一次资源，再提交整卡伤害并更新本局状态。</summary>
        private SkillActivationResult Activate(int cardId)
        {
            SkillActivationFailureReason reason = CheckConditions(cardId, out SkillCardState card);
            if (reason != SkillActivationFailureReason.None)
            {
                return Failure(reason, card);
            }

            var batch = new SkillDamageBatch(card.SourceItemInstanceId);
            reason = Prepare(card, batch);
            if (reason != SkillActivationFailureReason.None || (batch.Count > 0 && _damage == null))
            {
                return Failure(reason != SkillActivationFailureReason.None
                    ? reason : SkillActivationFailureReason.ActionFailed, card);
            }

            if (_energy != null && !_energy.TrySpend(card.EnergyCost, EnergySpendKind.Skill))
            {
                return Failure(SkillActivationFailureReason.InsufficientEnergy, card);
            }

            bool consumesItem = card.RemainingUses == 1;
            if (consumesItem && !_items.TryConsumeItemInstance(card.SourceItemInstanceId))
            {
                _energy?.AddEnergy(card.EnergyCost);
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

        /// <summary>先固定全部目标，再调用动作收集伤害；任何动作失败都不提交批次。</summary>
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
                requests.Add(new SkillExecutionRequest(definition, card.SourceItemInstanceId, origin, targets, batch));
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
            if (_energy != null && _energy.CurrentEnergy.CurrentValue < card.EnergyCost)
            {
                return SkillActivationFailureReason.InsufficientEnergy;
            }
            return SkillActivationFailureReason.None;
        }

        /// <summary>读取本次施法中心；无场景施法者的测试使用原点。</summary>
        private Vector3 CasterPosition() => _caster != null ? _caster.transform.position : Vector3.zero;

        /// <summary>推进本局全部卡牌冷却，包括已卸装实例。</summary>
        public void AdvanceCooldown(float deltaTime) => _hand?.AdvanceCooldowns(deltaTime);

        /// <summary>使用游戏时间推进冷却，暂停时停止。</summary>
        public void Tick() => AdvanceCooldown(Time.deltaTime);

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
