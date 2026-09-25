using System;
using R3;
using Services;
using UnityEngine;

namespace GameSystem
{
    /// <summary>
    /// 管理单局能量状态。主动行为使用 TrySpend，不可避免的消耗使用 Drain 并将能量限制在零以上。
    /// </summary>
    public sealed class EnergyService : IEnergyService, IEnergyCostService, IEnergyActionScope, IDisposable
    {
        private readonly ReactiveProperty<float> _currentEnergy = new();
        private float _consumptionMultiplier = 1f;

        public ReadOnlyReactiveProperty<float> CurrentEnergy => _currentEnergy;
        public float MaximumEnergy { get; private set; }
        public bool IsDepleted => _currentEnergy.CurrentValue <= 0f;
        public float ConsumptionMultiplier => _consumptionMultiplier;

        public event Action EnergyDepleted;

        private bool _depletionNotified;
        private int _actionDepth;

        /// <summary>创建可嵌套的同步动作边界，最后一个边界退出时再判断耗尽。</summary>
        public IDisposable BeginAction()
        {
            _actionDepth++;
            return new ActionLease(this);
        }

        private sealed class ActionLease : IDisposable
        {
            private EnergyService _owner;
            public ActionLease(EnergyService owner) => _owner = owner;

            /// <summary>只退出一次，退款恢复能量时不会触发终局。</summary>
            public void Dispose()
            {
                if (_owner == null)
                {
                    return;
                }
                EnergyService owner = _owner;
                _owner = null;
                owner._actionDepth--;
                owner.NotifyIfDepleted();
            }
        }

        /// <summary>
        /// 初始化本局能量，并将初始值限制在有效范围内。
        /// </summary>
        /// <param name="initialEnergy">初始能量。</param>
        /// <param name="maximumEnergy">最大能量。</param>
        public void Initialize(float initialEnergy, float maximumEnergy)
        {
            MaximumEnergy = NormalizeNonNegative(maximumEnergy);
            _currentEnergy.Value = Mathf.Clamp(NormalizeNonNegative(initialEnergy), 0f, MaximumEnergy);
            _consumptionMultiplier = 1f;
            _depletionNotified = false;
            NotifyIfDepleted();
        }

        /// <summary>设置本局所有能量消耗使用的倍率。</summary>
        /// <param name="multiplier">正数消耗倍率。</param>
        public void SetConsumptionMultiplier(float multiplier)
        {
            _consumptionMultiplier = IsPositiveFinite(multiplier) ? multiplier : 1f;
        }

        /// <summary>计算基础能量消耗在当前阶段的实际值。</summary>
        /// <param name="baseAmount">未修正的基础消耗。</param>
        /// <returns>应用当前倍率后的实际消耗。</returns>
        public float GetEffectiveCost(float baseAmount)
        {
            return NormalizeNonNegative(NormalizeNonNegative(baseAmount) * _consumptionMultiplier);
        }

        /// <summary>
        /// 在能量足够时原子性地扣除一次主动消耗。
        /// </summary>
        /// <param name="baseAmount">未修正的基础消耗量。</param>
        /// <param name="kind">消耗来源类型。</param>
        /// <returns>扣除成功或无需消耗时返回 true。</returns>
        public bool TrySpend(float baseAmount, EnergySpendKind kind)
        {
            float cost = GetEffectiveCost(baseAmount);
            if (cost <= 0f)
            {
                return true;
            }

            if (_currentEnergy.CurrentValue < cost)
            {
                return false;
            }

            _currentEnergy.Value -= cost;
            NotifyIfDepleted();
            return true;
        }

        /// <summary>
        /// 扣除一次不可避免的能量消耗，并将结果限制在零以上。
        /// </summary>
        /// <param name="baseAmount">未修正的基础消耗量。</param>
        /// <param name="kind">消耗来源类型。</param>
        /// <returns>实际扣除的能量。</returns>
        public float Drain(float baseAmount, EnergySpendKind kind)
        {
            float cost = GetEffectiveCost(baseAmount);
            if (cost <= 0f)
            {
                return 0f;
            }

            float previous = _currentEnergy.CurrentValue;
            _currentEnergy.Value = Mathf.Max(0f, previous - cost);
            NotifyIfDepleted();
            return previous - _currentEnergy.CurrentValue;
        }

        /// <summary>
        /// 增加能量，并将结果限制在最大能量以内。
        /// </summary>
        /// <param name="amount">要增加的能量。</param>
        /// <returns>实际增加的能量。</returns>
        public float AddEnergy(float amount)
        {
            float normalizedAmount = NormalizeNonNegative(amount);
            if (normalizedAmount <= 0f || MaximumEnergy <= 0f)
            {
                return 0f;
            }

            float previous = _currentEnergy.CurrentValue;
            _currentEnergy.Value = Mathf.Min(MaximumEnergy, previous + normalizedAmount);
            NotifyIfDepleted();
            return _currentEnergy.CurrentValue - previous;
        }

        /// <summary>
        /// 按时间流逝消耗本局能量。
        /// </summary>
        /// <param name="deltaTime">经过的秒数。</param>
        /// <param name="baseDrainPerSecond">每秒基础消耗量。</param>
        /// <returns>实际消耗的能量。</returns>
        public float Tick(float deltaTime, float baseDrainPerSecond)
        {
            float normalizedDeltaTime = NormalizeNonNegative(deltaTime);
            float normalizedDrain = NormalizeNonNegative(baseDrainPerSecond);
            return Drain(normalizedDeltaTime * normalizedDrain, EnergySpendKind.TimeFlow);
        }

        /// <summary>
        /// 释放响应式能量状态。
        /// </summary>
        public void Dispose()
        {
            _currentEnergy.Dispose();
        }

        /// <summary>
        /// 在动作边界外通知能量耗尽。
        /// </summary>
        private void NotifyIfDepleted()
        {
            if (_actionDepth > 0)
            {
                return;
            }
            if (!IsDepleted)
            {
                _depletionNotified = false;
                return;
            }

            if (_depletionNotified)
            {
                return;
            }

            _depletionNotified = true;
            EnergyDepleted?.Invoke();
        }

        private static float NormalizeNonNegative(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }

            return Mathf.Max(0f, value);
        }

        private static bool IsPositiveFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value) && value > 0f;
        }
    }
}
