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
    public sealed class PlayerCombatTarget : MonoBehaviour, IPlayerCombatTarget, IBuffTarget, IDamageRequestReceiver
    {
        [Inject] private IEnergyService _energy;
        [Inject] private IBuffService _buffService;

        // 仅供未经过容器的编辑器测试使用；正式运行时由 GameLoop 注入场景级服务。
        private static readonly BuffManner EditorFallbackBuffManner = new();

        /// <summary>玩家当前是否仍可作为战斗目标。</summary>
        public bool IsAlive => gameObject.activeInHierarchy &&
            (_energy == null || !_energy.IsDepleted);

        /// <summary>启用时向场景级 BuffManner 登记玩家目标。</summary>
        private void OnEnable()
        {
            RegisterBuffTarget();
        }

        /// <summary>注入完成后再次登记，覆盖容器晚于 OnEnable 注入的场景。</summary>
        private void Start()
        {
            RegisterBuffTarget();
        }

        /// <summary>禁用时从场景级 BuffManner 注销玩家目标。</summary>
        private void OnDisable()
        {
            GetBuffService()?.UnregisterTarget(this);
        }

        /// <summary>返回玩家当前移动速度倍率。</summary>
        public float GetMovementSpeedMultiplier()
        {
            RegisterBuffTarget();
            IBuffService buffService = GetBuffService();
            return buffService != null
                ? buffService.GetModifierMultiplier(
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
            GetBuffService()?.ApplyBuff(this, definition, sourceInstanceId);
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
            IBuffService buffService = GetBuffService();
            return buffService != null &&
                buffService.TryApplyBuffIfAbsent(this, definition, sourceInstanceId);
        }

        /// <summary>玩家目标始终允许接收有害 Buff。</summary>
        bool IBuffTarget.IsActive => isActiveAndEnabled && gameObject.activeInHierarchy;
        bool IBuffTarget.IsAlive => IsAlive;
        bool IBuffTarget.CanReceiveHarmfulEffects => true;

        /// <summary>玩家移动速度由调用方按需读取，因此这里只保留统一通知接缝。</summary>
        void IBuffTarget.OnBuffModifiersChanged(BuffModifierType modifierType)
        {
        }

        /// <summary>向场景级 BuffManner 登记玩家目标。</summary>
        private void RegisterBuffTarget()
        {
            GetBuffService()?.RegisterTarget(this);
        }

        /// <summary>取得容器服务，编辑器旧测试使用共享回退实例。</summary>
        private IBuffService GetBuffService()
        {
            if (_buffService != null)
            {
                return _buffService;
            }

            return !Application.isPlaying ? EditorFallbackBuffManner : null;
        }
    }
}
