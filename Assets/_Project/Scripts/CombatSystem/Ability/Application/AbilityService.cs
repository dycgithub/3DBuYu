using System.Collections.Generic;
using Interfaces;
using Services;
using SpatialSystem.Bridge;
using UnityEngine;
using VContainer.Unity;

namespace CombatSystem
{
    public sealed class AbilityService : ITickable
    {
        private readonly IEnergyService _energy;
        private readonly ICombatPhaseService _phase;
        private readonly IDamageApplier _damage;
        private readonly ISpatialQueryService _spatial;
        private readonly SkillHand _hand;
        private readonly SkillTargetPointer _pointer;
        private readonly ICombatItemConsumer _itemConsumer;
        private readonly Dictionary<int, float> _cooldowns = new();
        private readonly List<IDamageable> _targets = new();
        private readonly HashSet<IDamageable> _targetSet = new();

        public AbilityService(
            IEnergyService energy,
            ICombatPhaseService phase,
            IDamageApplier damage,
            ISpatialQueryService spatial,
            SkillHand hand,
            SkillTargetPointer pointer,
            ICombatItemConsumer itemConsumer)
        {
            _energy = energy;
            _phase = phase;
            _damage = damage;
            _spatial = spatial;
            _hand = hand;
            _pointer = pointer;
            _itemConsumer = itemConsumer;
        }

        public bool TryActivate(int sourceItemInstanceId)
        {
            SkillCardRuntime card = _hand?.FindBySourceItem(sourceItemInstanceId);
            SkillDefinition definition = card?.Definition;
            if (definition == null || _damage == null)
                return false;
            if (_phase != null && !_phase.CanPerformCombatActions)
                return false;

            int cardId = definition.GetInstanceID();
            if (_cooldowns.TryGetValue(cardId, out float remaining) && remaining > 0f)
                return false;

            if (definition.RequiresTargetPointer && !(_pointer?.HasTarget ?? false))
                return false;

            IDamageable selectedTarget = _pointer?.CurrentTarget;
            if (definition.RequiresTargetPointer && !selectedTarget.IsAliveAndValid())
                return false;
            if (_itemConsumer != null && !_itemConsumer.CanConsume(sourceItemInstanceId))
                return false;

            CollectTargets(selectedTarget);
            if (_targets.Count == 0)
                return false;

            if (_energy != null && !TrySpendEnergy(definition.EnergyCost))
                return false;

            int appliedCount = 0;
            for (int i = 0; i < _targets.Count; i++)
            {
                IDamageable target = _targets[i];
                if (!target.IsAliveAndValid())
                    continue;

                DamageRequest request = new DamageRequest
                {
                    AttackId = cardId,
                    SourceId = sourceItemInstanceId,
                    BaseDamage = Mathf.Max(0f, definition.Damage),
                    DamageType = definition.DamageType,
                    HitPoint = target.Position,
                    HitNormal = target.Position.sqrMagnitude > 0.0001f
                        ? target.Position.normalized
                        : Vector3.up
                };

                if (_damage.TryApply(target, request, out DamageResult result) && result.WasApplied)
                    appliedCount++;
            }

            if (appliedCount == 0)
                return false;
            if (_itemConsumer != null && !_itemConsumer.TryConsume(sourceItemInstanceId))
                return false;

            _hand.RemoveBySourceItem(sourceItemInstanceId);
            if (definition.Cooldown > 0f)
                _cooldowns[cardId] = definition.Cooldown;
            return true;
        }

        public void Tick()
        {
            float deltaTime = Mathf.Max(0f, Time.deltaTime);
            if (_cooldowns.Count == 0 || deltaTime <= 0f)
                return;

            List<int> expired = null;
            var updated = new List<KeyValuePair<int, float>>(_cooldowns);
            for (int i = 0; i < updated.Count; i++)
            {
                KeyValuePair<int, float> pair = updated[i];
                float next = pair.Value - deltaTime;
                if (next > 0f)
                {
                    _cooldowns[pair.Key] = next;
                    continue;
                }

                expired ??= new List<int>();
                expired.Add(pair.Key);
            }

            if (expired == null)
                return;
            for (int i = 0; i < expired.Count; i++)
                _cooldowns.Remove(expired[i]);
        }

        private bool TrySpendEnergy(float baseAmount)
        {
            if (baseAmount <= 0f)
                return true;
            if (!_energy.TrySpend(baseAmount, EnergySpendKind.Skill))
                return false;
            return _phase == null || _phase.CanPerformCombatActions;
        }

        private void CollectTargets(IDamageable selectedTarget)
        {
            _targets.Clear();
            _targetSet.Clear();

            if (_spatial != null)
            {
                List<IDamageable> queried = _spatial.QueryAll(SpatialRegistry.LAYER_ENEMY);
                if (queried != null)
                {
                    for (int i = 0; i < queried.Count; i++)
                    {
                        IDamageable target = queried[i];
                        if (target.IsAliveAndValid() && _targetSet.Add(target))
                            _targets.Add(target);
                    }
                }
            }

            if (selectedTarget.IsAliveAndValid() && _targetSet.Add(selectedTarget))
                _targets.Add(selectedTarget);
        }
    }
}
