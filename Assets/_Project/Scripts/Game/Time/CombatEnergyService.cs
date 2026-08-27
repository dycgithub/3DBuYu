using System;
using R3;
using Services;
using UnityEngine;

namespace GameSystem
{
    /// <summary>
    /// Per-run energy state. Active actions use TrySpend while unavoidable drains clamp at zero.
    /// </summary>
    public sealed class CombatEnergyService : ICombatEnergyService, IDisposable
    {
        private readonly ReactiveProperty<float> _currentEnergy = new();

        public ReadOnlyReactiveProperty<float> CurrentEnergy => _currentEnergy;
        public float MaximumEnergy { get; private set; }
        public float CostMultiplier { get; private set; } = 1f;
        public bool IsDepleted => _currentEnergy.CurrentValue <= 0f;

        public event Action EnergyDepleted;

        private bool _depletionNotified;

        public void Initialize(float initialEnergy, float maximumEnergy)
        {
            MaximumEnergy = NormalizeNonNegative(maximumEnergy);
            _currentEnergy.Value = Mathf.Clamp(NormalizeNonNegative(initialEnergy), 0f, MaximumEnergy);
            CostMultiplier = 1f;
            _depletionNotified = false;
            NotifyIfDepleted();
        }

        public void SetCostMultiplier(float multiplier)
        {
            CostMultiplier = Mathf.Max(1f, NormalizeNonNegative(multiplier));
        }

        public bool TrySpend(float baseAmount, EnergySpendKind kind)
        {
            float cost = GetAdjustedCost(baseAmount);
            if (cost <= 0f)
                return true;

            if (_currentEnergy.CurrentValue < cost)
                return false;

            _currentEnergy.Value -= cost;
            NotifyIfDepleted();
            return true;
        }

        public float Drain(float baseAmount, EnergySpendKind kind)
        {
            float cost = GetAdjustedCost(baseAmount);
            if (cost <= 0f)
                return 0f;

            float previous = _currentEnergy.CurrentValue;
            _currentEnergy.Value = Mathf.Max(0f, previous - cost);
            NotifyIfDepleted();
            return previous - _currentEnergy.CurrentValue;
        }

        public float AddEnergy(float amount)
        {
            float normalizedAmount = NormalizeNonNegative(amount);
            if (normalizedAmount <= 0f || MaximumEnergy <= 0f)
                return 0f;

            float previous = _currentEnergy.CurrentValue;
            _currentEnergy.Value = Mathf.Min(MaximumEnergy, previous + normalizedAmount);
            NotifyIfDepleted();
            return _currentEnergy.CurrentValue - previous;
        }

        public float Tick(float deltaTime, float baseDrainPerSecond)
        {
            float normalizedDeltaTime = NormalizeNonNegative(deltaTime);
            float normalizedDrain = NormalizeNonNegative(baseDrainPerSecond);
            return Drain(normalizedDeltaTime * normalizedDrain, EnergySpendKind.TimeFlow);
        }

        public void Dispose()
        {
            _currentEnergy.Dispose();
        }

        private float GetAdjustedCost(float baseAmount)
        {
            return NormalizeNonNegative(baseAmount) * CostMultiplier;
        }

        private void NotifyIfDepleted()
        {
            if (!IsDepleted)
            {
                _depletionNotified = false;
                return;
            }

            if (_depletionNotified)
                return;

            _depletionNotified = true;
            EnergyDepleted?.Invoke();
        }

        private static float NormalizeNonNegative(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                return 0f;

            return Mathf.Max(0f, value);
        }
    }
}
