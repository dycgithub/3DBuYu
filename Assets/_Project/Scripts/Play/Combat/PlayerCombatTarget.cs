using CombatSystem;
using Interfaces;
using Services;
using UnityEngine;
using VContainer;

namespace Play
{
    /// <summary>
    /// 将玩家的能量资源适配为通用战斗目标。
    /// 敌人不需要知道玩家控制器或能量服务的具体实现。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BuffController))]
    public sealed class PlayerCombatTarget : MonoBehaviour, IPlayerCombatTarget, IDamageRequestReceiver
    {
        [Inject] private IEnergyService _energy;

        private BuffController _buffController;

        /// <summary>玩家当前是否仍可作为战斗目标。</summary>
        public bool IsAlive => gameObject.activeInHierarchy &&
            (_energy == null || !_energy.IsDepleted);

        /// <summary>缓存玩家 Buff 控制器。</summary>
        private void Awake()
        {
            _buffController = GetComponent<BuffController>();
        }

        /// <summary>返回玩家当前移动速度倍率。</summary>
        public float GetMovementSpeedMultiplier()
        {
            return _buffController != null
                ? _buffController.GetModifierMultiplier(BuffModifierType.MovementSpeedMultiplier)
                : 1f;
        }

        /// <summary>将伤害转换为能量消耗。</summary>
        /// <param name="request">伤害请求。</param>
        /// <returns>玩家能量层面的处理结果。</returns>
        public DamageResult ReceiveDamageRequest(in DamageRequest request)
        {
            if (!IsAlive || _energy == null || request.DamageAmount <= 0f)
            {
                return new DamageResult
                {
                    Outcome = DamageOutcome.Invalid,
                    RemainingHealth = _energy != null ? _energy.CurrentEnergy.CurrentValue : 0f
                };
            }

            float appliedDamage = _energy.Drain(
                request.DamageAmount,
                EnergySpendKind.EnemyContact);
            float remainingEnergy = _energy.CurrentEnergy.CurrentValue;
            bool killed = _energy.IsDepleted;

            return new DamageResult
            {
                Outcome = killed ? DamageOutcome.Killed : DamageOutcome.Applied,
                AppliedDamage = appliedDamage,
                RemainingHealth = remainingEnergy,
                WasKilled = killed
            };
        }

        /// <summary>恢复玩家能量并返回实际恢复量。</summary>
        public float Heal(float amount)
        {
            return _energy == null ? 0f : _energy.AddEnergy(Mathf.Max(0f, amount));
        }

        /// <summary>玩家属于玩家目标阵营。</summary>
        CombatTargetKind ICombatTarget.TargetKind => CombatTargetKind.Player;

        /// <summary>玩家当前位置。</summary>
        Vector3 IDamageable.Position => transform.position;

        /// <summary>玩家是否仍然存活。</summary>
        bool IDamageable.IsAlive => IsAlive;

        /// <summary>玩家 Transform。</summary>
        Transform IDamageable.Transform => transform;

        /// <summary>以默认来源给玩家施加 Buff。</summary>
        public void ApplyBuff(BuffDefinitionSO definition)
        {
            ApplyBuff(definition, 0);
        }

        /// <summary>以指定来源给玩家施加 Buff。</summary>
        public void ApplyBuff(BuffDefinitionSO definition, int sourceInstanceId)
        {
            _buffController?.ApplyBuff(definition, sourceInstanceId);
        }
    }
}
