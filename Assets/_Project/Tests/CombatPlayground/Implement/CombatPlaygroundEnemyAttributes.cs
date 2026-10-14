using System.Collections.Generic;
using CombatSystem;
using EnemySystem;
using UnityEngine;
using _Project.Tests.CombatPlayground.Model;

namespace _Project.Tests.CombatPlayground.Implement
{
    /// <summary>只对当前实验实例调整生命、移动和限时战斗倍率。</summary>
    public sealed class CombatPlaygroundEnemyAttributes : MonoBehaviour
    {
        private const float MinimumHealthMultiplier = 0.5f;
        private const float MaximumHealthMultiplier = 10f;
        private const float MaximumAttributeMultiplier = 3f;
        private const float MinimumBuffDuration = 1f;
        private const float MaximumBuffDuration = 30f;
        private const float DefaultBuffDuration = 10f;

        [SerializeField] private GameObject visualEffectPrefab;

        private IBuffService _buffService;

        private readonly Dictionary<Enemy, List<BuffDefinitionSO>> _definitionsByTarget = new();
        private float _healthMultiplier = 1f;
        private float _movementMultiplier = 1f;
        private float _damageTakenMultiplier = 1f;
        private float _attackDamageMultiplier = 1f;
        private float _attackAvailabilityMultiplier = 1f;
        private float _buffDurationSeconds = DefaultBuffDuration;

        /// <summary>获取当前面板设置，供 UI 在重置后恢复控件值。</summary>
        internal CombatPlaygroundEnemyTuningSnapshot Snapshot => new(
            _healthMultiplier,
            _movementMultiplier,
            _damageTakenMultiplier,
            _attackDamageMultiplier,
            _attackAvailabilityMultiplier,
            _buffDurationSeconds);

        /// <summary>设置选中敌人的临时生命倍率。</summary>
        internal void SetHealthMultiplier(float value)
        {
            _healthMultiplier = Mathf.Clamp(value, MinimumHealthMultiplier, MaximumHealthMultiplier);
        }

        /// <summary>设置选中敌人的生成移动倍率。</summary>
        internal void SetMovementMultiplier(float value)
        {
            _movementMultiplier = Mathf.Clamp(value, 0f, MaximumAttributeMultiplier);
        }

        /// <summary>设置选中敌人的限时承伤倍率。</summary>
        internal void SetDamageTakenMultiplier(float value)
        {
            _damageTakenMultiplier = Mathf.Clamp(value, 0f, MaximumAttributeMultiplier);
        }

        /// <summary>设置选中敌人的限时攻击伤害倍率。</summary>
        internal void SetAttackDamageMultiplier(float value)
        {
            _attackDamageMultiplier = Mathf.Clamp(value, 0f, MaximumAttributeMultiplier);
        }

        /// <summary>设置选中敌人的限时攻击可用倍率。</summary>
        internal void SetAttackAllowedMultiplier(float value)
        {
            _attackAvailabilityMultiplier = Mathf.Clamp01(value);
        }

        /// <summary>设置测试 Buff 的有效时长。</summary>
        internal void SetBuffDuration(float value)
        {
            _buffDurationSeconds = Mathf.Clamp(value, MinimumBuffDuration, MaximumBuffDuration);
        }

        /// <summary>应用选中敌人的会话属性与限时战斗倍率，不改写静态配置资产。</summary>
        internal bool Apply(Enemy target, IBuffService buffService)
        {
            if (target == null || buffService == null)
            {
                return false;
            }

            _buffService = buffService;
            ClearTarget(target);
            if (target.IsDead)
            {
                target.ResetForReuse();
            }

            target.InitializeForSpawn(_healthMultiplier, _movementMultiplier);
            var definitions = new List<BuffDefinitionSO>(3);
            AddBuff(target, definitions, BuffModifierType.DamageTakenMultiplier,
                _damageTakenMultiplier, "DamageTaken", visualEffectPrefab);
            AddBuff(target, definitions, BuffModifierType.AttackDamage,
                _attackDamageMultiplier, "AttackDamage", null);
            AddBuff(target, definitions, BuffModifierType.AttackAvailability,
                _attackAvailabilityMultiplier, "AttackAvailability", null);

            if (definitions.Count > 0)
            {
                _definitionsByTarget[target] = definitions;
            }

            return true;
        }

        /// <summary>移除面板施加的 Buff，并恢复所有调节值。</summary>
        internal void ResetAll()
        {
            var targets = new List<Enemy>(_definitionsByTarget.Keys);
            for (int index = 0; index < targets.Count; index++)
            {
                ClearTarget(targets[index]);
            }

            _healthMultiplier = 1f;
            _movementMultiplier = 1f;
            _damageTakenMultiplier = 1f;
            _attackDamageMultiplier = 1f;
            _attackAvailabilityMultiplier = 1f;
            _buffDurationSeconds = DefaultBuffDuration;
        }

        /// <summary>释放会话内创建的临时 Buff 定义。</summary>
        private void OnDestroy()
        {
            ResetAll();
        }

        private void AddBuff(
            Enemy target,
            List<BuffDefinitionSO> definitions,
            BuffModifierType modifierType,
            float multiplier,
            string groupId,
            GameObject effectPrefab)
        {
            if (Mathf.Approximately(multiplier, 1f))
            {
                return;
            }

            BuffDefinitionSO definition = ScriptableObject.CreateInstance<BuffDefinitionSO>();
            definition.name = $"CombatPlayground_{groupId}";
            definition.ModifierType = modifierType;
            definition.StackGroupId = groupId;
            definition.Polarity = BuffPolarity.Neutral;
            definition.DurationSeconds = _buffDurationSeconds;
            definition.ModifierValue = multiplier;
            definition.StackPolicy = BuffStackPolicy.Replace;
            definition.VisualEffectPrefab = effectPrefab;
            definitions.Add(definition);
            _buffService.ApplyBuff(target, definition, GetInstanceID());
        }

        private void ClearTarget(Enemy target)
        {
            _buffService?.RemoveBySourceId(target, GetInstanceID());
            if (!_definitionsByTarget.Remove(target, out List<BuffDefinitionSO> definitions))
            {
                return;
            }

            DestroyDefinitions(definitions);
        }

        private static void DestroyDefinitions(List<BuffDefinitionSO> definitions)
        {
            for (int index = 0; index < definitions.Count; index++)
            {
                BuffDefinitionSO definition = definitions[index];
                if (definition == null)
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(definition);
                }
                else
                {
                    DestroyImmediate(definition);
                }
            }

            definitions.Clear();
        }
    }
}
