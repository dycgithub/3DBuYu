using CombatSystem;
using Interfaces;
using Services;
using UnityEngine;
using VContainer;

namespace Play
{
    /// <summary>
    /// 将玩家的能量资源适配为通用战斗目标。
    /// Buff 生命周期仅通过注入的场景级服务登记、注销和查询。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCombatTarget : MonoBehaviour, IPlayerCombatTarget, IBuffTarget, IDamageRequestReceiver
    {
        [Inject] private IEnergyService _energy;
        [Inject] private IBuffService _buffService;
        private bool _energyEventSubscribed;

        /// <summary>玩家当前是否仍可作为战斗目标。</summary>
        public bool IsAlive => gameObject.activeInHierarchy &&
            (_energy == null || !_energy.IsDepleted);

        /// <summary>启用时订阅能量耗尽并登记存活玩家。</summary>
        private void OnEnable()
        {
            SubscribeEnergyDepleted();
            RegisterBuffTarget();
        }

        /// <summary>注入完成后订阅能量并再次登记，覆盖容器晚于 OnEnable 注入的场景。</summary>
        private void Start()
        {
            SubscribeEnergyDepleted();
            RegisterBuffTarget();
        }

        /// <summary>禁用时解除能量订阅并注销玩家 Buff。</summary>
        private void OnDisable()
        {
            UnsubscribeEnergyDepleted();
            _buffService?.UnregisterTarget(this);
        }

        /// <summary>能量耗尽时注销玩家 Buff，回收附着视效。</summary>
        private void OnEnergyDepleted()
        {
            _buffService?.UnregisterTarget(this);
        }

        private void SubscribeEnergyDepleted()
        {
            if (_energy == null || _energyEventSubscribed)
            {
                return;
            }

            _energy.EnergyDepleted += OnEnergyDepleted;
            _energyEventSubscribed = true;
        }

        private void UnsubscribeEnergyDepleted()
        {
            if (_energy == null || !_energyEventSubscribed)
            {
                return;
            }

            _energy.EnergyDepleted -= OnEnergyDepleted;
            _energyEventSubscribed = false;
        }

        /// <summary>返回玩家当前移动速度倍率。</summary>
        public float GetMovementSpeedMultiplier()
        {
            RegisterBuffTarget();
            return _buffService != null
                ? _buffService.GetModifierMultiplier(
                    this,
                    BuffModifierType.MovementSpeedMultiplier)
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
            if (definition == null)
            {
                return;
            }

            RegisterBuffTarget();
            _buffService?.ApplyBuff(this, definition, sourceInstanceId);
        }

        /// <summary>
        /// 仅在玩家没有同类活动 Buff 时施加一个 Buff。
        /// </summary>
        /// <param name="definition">要施加的 Buff 静态定义。</param>
        /// <param name="sourceInstanceId">Buff 来源实例标识。</param>
        /// <returns>本次实际新增 Buff 时返回 true。</returns>
        public bool TryApplyBuffIfAbsent(BuffDefinitionSO definition, int sourceInstanceId)
        {
            if (definition == null)
            {
                return false;
            }

            RegisterBuffTarget();
            return _buffService != null &&
                _buffService.TryApplyBuffIfAbsent(this, definition, sourceInstanceId);
        }

        /// <summary>玩家目标始终允许接收有害 Buff。</summary>
        bool IBuffTarget.IsActive => isActiveAndEnabled && gameObject.activeInHierarchy;
        bool IBuffTarget.IsAlive => IsAlive;
        bool IBuffTarget.CanReceiveHarmfulEffects => true;

        /// <summary>玩家移动速度由调用方按需读取，因此这里只保留统一通知接缝。</summary>
        void IBuffTarget.OnBuffModifiersChanged(BuffModifierType modifierType)
        {
        }

        /// <summary>向场景级 BuffService 登记玩家目标。</summary>
        private void RegisterBuffTarget()
        {
            _buffService?.RegisterTarget(this);
        }
    }
}
